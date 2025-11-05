using BBT.Workflow.Definitions;
using BBT.Workflow.SubFlow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BBT.Workflow.Orchestration.Controllers.Utilities;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[ServiceFilter(typeof(ResponseHeaderFilter))]
public sealed class UtilityController(
    IAdminAppService adminAppService,
    ISubflowCompletionService subflowCompletionService,
    ILogger<UtilityController> logger) : ControllerBase
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("utilities/invalidate")]
    public async Task<IActionResult> InvalidateCacheAsync(
        [FromBody] InvalidateCacheInput input,
        CancellationToken cancellationToken = default)
    {
        await adminAppService.InvalidateCacheAsync(input, cancellationToken);
        return Ok(new { result = "ok" });
    }
    
    /// <summary>
    /// Handles flow completion events from subflows.
    /// This endpoint receives notifications when a subflow completes and triggers
    /// the parent workflow to continue with output mapping and automatic transitions.
    /// </summary>
    /// <param name="completedDataEto">The completed flow data containing instance information and final data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Status indicating the completion handling result</returns>
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("utilities/flow/completed")]
    public async Task<IActionResult> HandleFlowCompletedAsync(
        [FromBody] FlowCompletedDataEto completedDataEto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Received flow completion event for instance {InstanceId} in domain {Domain}, workflow {WorkflowName}",
                completedDataEto.InstanceId, completedDataEto.Domain, completedDataEto.Workflow);

            await subflowCompletionService.HandleSubFlowCompletionAsync(completedDataEto, cancellationToken);
            
            logger.LogInformation(
                "Successfully processed flow completion for instance {InstanceId}",
                completedDataEto.InstanceId);

            return Ok(new { result = "ok", message = "Flow completion processed successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to process flow completion for instance {InstanceId} in domain {Domain}",
                completedDataEto.InstanceId, completedDataEto.Domain);
                
            return StatusCode(500, new { error = "Failed to process flow completion", message = ex.Message });
        }
    }
} 