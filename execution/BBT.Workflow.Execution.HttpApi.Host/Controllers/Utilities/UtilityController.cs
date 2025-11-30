using BBT.Aether.AspNetCore.Controllers;
using BBT.Workflow.Definitions;
using Microsoft.AspNetCore.Mvc;

namespace BBT.Workflow.Execution.Controllers.Utilities;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
public sealed class UtilityController(
    IAdminAppService appService) : AetherControllerBase
{
    ///api/v1/utilities/invalidate
    /// <summary>
    /// Handles cache invalidation messages from the orchestration service.
    /// This method is triggered when the system components workflow publishes cache invalidation events.
    /// </summary>
    /// <param name="input">The cache invalidation request data.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>HTTP 200 OK when cache invalidation is successful.</returns>
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("utilities/invalidate")]
    public async Task<IActionResult> InvalidateCacheAsync(
        [FromBody] InvalidateCacheInput input,
        CancellationToken cancellationToken = default)
    {
        var result = await appService.InvalidateCacheAsync(input, cancellationToken);
        return FromResult(result);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("utilities/re-initialize")]
    public async Task<IActionResult> ReInitializeAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await appService.ReInitializeAsync(cancellationToken);
        return FromResult(result);
    }
}