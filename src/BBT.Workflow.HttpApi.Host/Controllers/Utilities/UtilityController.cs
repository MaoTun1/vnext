using BBT.Workflow.Instances;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BBT.Workflow.Controllers.Utilities;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[ServiceFilter(typeof(ResponseHeaderFilter))]
public sealed class UtilityController(
    IInstanceQueryAppService queryAppService) : ControllerBase
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("{domain}/workflows/{workflow}/instances/{instance}/transitions/available")]
    public async Task<IActionResult> GetAvailableTransitionsAsync(
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromRoute] string instance,
        [FromQuery] string? version,
        CancellationToken cancellationToken = default)
    {
        var input = new GetAvailableTransitionInput
        {
            Domain = domain,
            Workflow = workflow,
            Instance = instance,
            Version = version
        };

        var response = await queryAppService.GetAvailableTransitionsAsync(input, cancellationToken);
        return Ok(response.Data);
    }
}