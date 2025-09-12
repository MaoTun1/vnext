using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Workflow.Instances;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ExecutionContext = BBT.Workflow.Shared.ExecutionContext;

namespace BBT.Workflow.Orchestration.Controllers.Instances;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[ServiceFilter(typeof(ResponseHeaderFilter))]
public sealed class InstanceController(
    IInstanceCommandAppService commandAppService,
    IInstanceQueryAppService queryAppService,
    IHttpContextAccessor httpContextAccessor) : ControllerBase
{
    [HttpPost("{domain}/workflows/{workflow}/instances/start")]
    public async Task<IActionResult> StartAsync(
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromBody] CreateInstanceDto request,
        [FromQuery] string? version = null,
        [FromQuery] bool sync = false,
        CancellationToken cancellationToken = default
    )
    {
        var input = new StartInstanceInput(domain, workflow, version, sync)
        {
            Instance = new CreateInstanceInput
            {
                Key = request.Key,
                Tags = request.Tags,
                Attributes = request.Attributes
            }
        };
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            input.Headers = httpContext.Request.Headers.ToDictionary(s => s.Key.ToLower(), s => s.Value.ToString());
            input.RouteValues = httpContext.Request.RouteValues.ToDictionary(s => s.Key, s => s.Value?.ToString());
        }
        
        var response = await commandAppService.StartAsync(input, cancellationToken);
        return Ok(response.Data);
    }
    
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("{domain}/workflows/{workflow}/sub/instances/start")]
    public async Task<IActionResult> StartSubAsync(
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromBody] CreateSubInstanceDto request,
        [FromQuery] string? version = null,
        [FromQuery] bool sync = false,
        CancellationToken cancellationToken = default
    )
    {
        var input = new StartInstanceInput(domain, workflow, version, sync)
        {
            Instance = new CreateInstanceInput
            {
                Id = request.Id,
                Key = request.Key,
                Tags = request.Tags,
                Attributes = request.Attributes,
                Callback = request.Callback,
                MetaData = new ObjectDictionary(request.MetaData)
            }
        };
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            input.Headers = httpContext.Request.Headers.ToDictionary(s => s.Key.ToLower(), s => s.Value.ToString());
            input.RouteValues = httpContext.Request.RouteValues.ToDictionary(s => s.Key, s => s.Value?.ToString());
        }
        
        var response = await commandAppService.StartAsync(input, cancellationToken);
        return Ok(response.Data);
    }

    [HttpPatch("{domain}/workflows/{workflow}/instances/{instanceId}/transitions/{transitionKey}")]
    public async Task<IActionResult> TransitionAsync(
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromRoute] Guid instanceId,
        [FromRoute] string transitionKey,
        [FromBody] JsonElement? attributes,
        [FromQuery] string? version = null,
        [FromQuery] bool sync = false,
        CancellationToken cancellationToken = default
    )
    {
        if (version.IsNullOrEmpty())
        {
            var flowInfo = HttpContext.GetWorkflowInfo();
            version = flowInfo?.Version;
        }

        var input = new TransitionInput(domain, workflow, version, attributes, sync);
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            input.Headers = httpContext.Request.Headers.ToDictionary(s => s.Key.ToLower(), s => s.Value.ToString());
            input.RouteValues = httpContext.Request.RouteValues.ToDictionary(s => s.Key, s => s.Value?.ToString());
        }
        
        var response = await commandAppService.TransitionAsync(
            instanceId,
            transitionKey,
            input,
            cancellationToken);
        return Ok(response.Data);
    }
    
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPatch("{domain}/workflows/{workflow}/instances/{instanceId}/transitions/{transitionKey}/auto")]
    public async Task<IActionResult> AutoTransitionAsync(
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromRoute] Guid instanceId,
        [FromRoute] string transitionKey,
        [FromBody] JsonElement? attributes,
        [FromQuery] string? version = null,
        [FromQuery] bool sync = false,
        CancellationToken cancellationToken = default
    )
    {
        if (version.IsNullOrEmpty())
        {
            var flowInfo = HttpContext.GetWorkflowInfo();
            version = flowInfo?.Version;
        }

        var input = new TransitionInput(domain, workflow, version, attributes, sync)
        {
            ExecutionContext = ExecutionContext.System
        };
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            input.Headers = httpContext.Request.Headers.ToDictionary(s => s.Key.ToLower(), s => s.Value.ToString());
            input.RouteValues = httpContext.Request.RouteValues.ToDictionary(s => s.Key, s => s.Value?.ToString());
        }
        
        var response = await commandAppService.TransitionAsync(
            instanceId,
            transitionKey,
            input,
            cancellationToken);
        return Ok(response.Data);
    }

    [HttpGet("{domain}/workflows/{workflow}/instances/{instance}")]
    public async Task<IActionResult> GetInstanceAsync(
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromRoute] string instance,
        [FromQuery] string[]? extension = null,
        CancellationToken cancellationToken = default)
    {
        var input = new GetInstanceInput
        {
            Domain = domain,
            Workflow = workflow,
            Instance = instance,
            Extension = extension,
            IfNoneMatch = ifNoneMatch
        };

        var result = await queryAppService.GetInstanceAsync(input, cancellationToken);

        if (result.IsNotModified)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(result.Data);
    }

    [HttpGet("{domain}/workflows/{workflow}/instances")]
    public async Task<IActionResult> GetInstanceListAsync(
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromQuery] string[]? filter = null,
        [FromQuery] string[]? extension = null,
        [FromQuery] [Range(1, 1000)] int page = 1,
        [FromQuery] [Range(1, 100)] int pageSize = 10,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var input = new GetInstanceListInput
        {

            Domain = domain,
            Workflow = workflow,
            Extension = extension,
            Page = page,
            PageSize = pageSize,
            PageUrl = $"{domain}/workflows/{workflow}/instances",
            
            Filter = filter,
            Sort = sort
        };

        var response = await queryAppService.GetInstanceListAsync(input, cancellationToken);
        return Ok(response.Data);
    }

    [HttpGet("{domain}/workflows/{workflow}/instances/{instance}/transitions")]
    public async Task<IActionResult> GetInstanceHistoryAsync(
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromRoute] string instance,
        [FromQuery] string[]? extension = null,
        CancellationToken cancellationToken = default)
    {
        var input = new GetInstanceHistoryInput
        {
            Domain = domain,
            Workflow = workflow,
            Instance = instance,
            Extension = extension
        };

        var response = await queryAppService.GetInstanceHistoryAsync(input, cancellationToken);
        return Ok(response.Data);
    }
} 