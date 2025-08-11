using BBT.Workflow.Definitions;
using BBT.Workflow.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Execution.Controllers.Utilities;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
public sealed class UtilityController(
    IAdminAppService appService,
    IOptions<RuntimeOptions> runtimeOptions) : ControllerBase
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
        await appService.InvalidateCacheAsync(input, cancellationToken);
        return Ok(new { result = "ok", message = "Cache invalidated successfully" });
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("utilities/re-initialize")]
    public async Task<IActionResult> ReInitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await appService.ReInitializeAsync(cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Test endpoint to verify RuntimeOptions configuration is loaded correctly.
    /// Returns the current RuntimeOptions values to verify configuration binding.
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("utilities/runtime-config")]
    public IActionResult GetRuntimeConfigAsync()
    {
        return Ok(new 
        { 
            EnableSchemaMigration = runtimeOptions.Value.EnableSchemaMigration,
            SchemasCount = runtimeOptions.Value.Schemas.Count,
            SchemaNames = runtimeOptions.Value.Schemas.Keys.ToArray()
        });
    }
}