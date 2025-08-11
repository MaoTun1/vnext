using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Workflow.Instances;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BBT.Workflow.Controllers.Instances;

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
        [FromBody] CreateInstanceInput request,
        [FromQuery] string? version = null,
        [FromQuery] bool sync = false,
        CancellationToken cancellationToken = default
    )
    {
        var input = new StartInstanceInput(domain, workflow, version, sync)
        {
            Instance = request
        };
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            input.Headers = httpContext.Request.Headers.ToDictionary(s => s.Key, s => s.Value.ToString());
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
        /*
            TODO: Version handling -
            Start Instance adımında versiyon opsiyonel ve mutluaka bir version ile işlem sonlandırılır.
            X-Workflow header'ında versiyon bilgisi döner.
            Transition tetiklendiğinde X-Workflow headerı MUTLAKA beklenir,
            çünkü versiyon bilgisini göndermediği durumda instance hangi versiyonla başladı bilmez.
            Workflow tanımı için başlatılan versiyonla ilerlemesi için gereklidir?
        */
        if (version.IsNullOrEmpty())
        {
            var flowInfo = HttpContext.GetWorkflowInfo();
            version = flowInfo?.Version;
        }

        var input = new TransitionInput(domain, workflow, version, attributes, sync);
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            input.Headers = httpContext.Request.Headers.ToDictionary(s => s.Key, s => s.Value.ToString());
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
        [FromQuery] string[]? extension = null,
        [FromQuery] string[]? filter = null,
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
            Filter = filter,
            Page = page,
            PageSize = pageSize,
            PageUrl = $"{domain}/workflows/{workflow}/instances",
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