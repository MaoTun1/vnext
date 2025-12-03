using BBT.Aether.AspNetCore.Controllers;
using BBT.Workflow.Definitions;
using Microsoft.AspNetCore.Mvc;

namespace BBT.Workflow.Orchestration.Controllers.Definitions;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
public sealed class AdminController(IAdminAppService appService) : AetherControllerBase
{
    [HttpPost("publish")]
    public async Task<IActionResult> PublishAsync(
        [FromBody] PublishInput input,
        CancellationToken cancellationToken = default)
    {
        var result = await appService.PublishAsync(input, cancellationToken);
        return FromResult(result);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("re-initialize")]
    public async Task<IActionResult> ReInitializeAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await appService.ReInitializeAsync(cancellationToken);
        return FromResult(result);
    }
} 