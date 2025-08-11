using BBT.Workflow.Definitions;
using Microsoft.AspNetCore.Mvc;

namespace BBT.Workflow.Controllers.Definitions;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
public sealed class AdminController(IAdminAppService appService) : ControllerBase
{
    [HttpPost("publish")]
    public async Task<IActionResult> PublishAsync(
        [FromBody] PublishInput input,
        CancellationToken cancellationToken = default)
    {
        await appService.PublishAsync(input, cancellationToken);
        return Ok();
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("invalidate")]
    public async Task<IActionResult> InvalidateCacheAsync(
        [FromBody] InvalidateCacheInput input,
        CancellationToken cancellationToken = default)
    {
        await appService.InvalidateCacheAsync(input, cancellationToken);
        return Ok(new { result = "ok" });
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("re-initialize")]
    public async Task<IActionResult> ReInitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await appService.ReInitializeAsync(cancellationToken);
        return Ok();
    }
}