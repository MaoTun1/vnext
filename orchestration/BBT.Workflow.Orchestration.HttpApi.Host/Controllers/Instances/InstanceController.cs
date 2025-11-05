using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Workflow.HttpApi.Shared;
using BBT.Workflow.Instances;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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
    /// <summary>
    /// Starts a new workflow instance.
    /// </summary>
    /// <response code="200">Instance started successfully</response>
    /// <response code="400">Validation failed</response>
    /// <response code="404">Workflow or state not found</response>
    /// <response code="409">Instance with same key already exists</response>
    [HttpPost("{domain}/workflows/{workflow}/instances/start")]
    [ProducesResponseType(typeof(StartInstanceOutput), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
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
            input.Headers = httpContext.Request.Headers.ToDictionary(s => s.Key.ToLower(), s => s.Value.FirstOrDefault()?.ToString());
            input.RouteValues = httpContext.Request.RouteValues.ToDictionary(s => s.Key, s => s.Value?.ToString());
        }

        var result = await commandAppService.StartAsync(input, cancellationToken);
        
        return result.ToActionResult();
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
            input.Headers = httpContext.Request.Headers.ToDictionary(s => s.Key.ToLower(), s => s.Value.FirstOrDefault()?.ToString());
            input.RouteValues = httpContext.Request.RouteValues.ToDictionary(s => s.Key, s => s.Value?.ToString());
        }

        var result = await commandAppService.StartAsync(input, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Executes a transition on a workflow instance.
    /// </summary>
    /// <response code="200">Transition executed successfully</response>
    /// <response code="400">Validation or state transition rule failed</response>
    /// <response code="403">Transition not authorized for current context</response>
    /// <response code="404">Instance, workflow, or transition not found</response>
    /// <response code="409">Transition already in progress (locked) or SubFlow blocking</response>
    /// <response code="503">Service temporarily unavailable</response>
    [HttpPatch("{domain}/workflows/{workflow}/instances/{instanceId}/transitions/{transitionKey}")]
    [ProducesResponseType(typeof(TransitionOutput), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
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
            input.Headers = httpContext.Request.Headers.ToDictionary(s => s.Key.ToLower(), s => s.Value.FirstOrDefault()?.ToString());
            input.RouteValues = httpContext.Request.RouteValues.ToDictionary(s => s.Key, s => s.Value?.ToString());
        }

        var result = await commandAppService.TransitionAsync(
            instanceId,
            transitionKey,
            input,
            cancellationToken);
        
        return result.ToActionResult();
    }
    
    /// <summary>
    /// Retrieves a workflow instance by key or ID.
    /// </summary>
    /// <response code="200">Instance retrieved successfully</response>
    /// <response code="304">Not modified (ETag match)</response>
    /// <response code="404">Instance not found</response>
    [HttpGet("{domain}/workflows/{workflow}/instances/{instance}")]
    [ProducesResponseType(typeof(GetInstanceOutput), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
        return result.ToActionResult();
    }

    [HttpGet("{domain}/workflows/{workflow}/instances")]
    public async Task<IActionResult> GetInstanceListAsync(
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromQuery] string[]? filter = null,
        [FromQuery] string[]? extension = null,
        [FromQuery][Range(1, 1000)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 10,
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
        return response.ToActionResult();
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
        return response.ToActionResult();
    }
    [ApiExplorerSettings(IgnoreApi = true)]
      [HttpGet("{domain}/workflows/{workflow}/instances/{instance}/data")]
    
    public async Task<IActionResult> GetInstanceDataAsync(
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromRoute] string instance,
        CancellationToken cancellationToken = default)
    {
        var input = new GetInstanceDataInput
        {
            Domain = domain,
            Workflow = workflow,
            Instance = instance,
            IfNoneMatch = ifNoneMatch
        };
 
        var result = await queryAppService.GetInstanceDataAsync(input, cancellationToken);
        return result.ToActionResult();
    }
} 