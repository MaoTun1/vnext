using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.SubFlow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Orchestration.Controllers.Utilities;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[ServiceFilter(typeof(ResponseHeaderFilter))]
public sealed class UtilityController(
    IInstanceQueryAppService queryAppService,
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
    
    
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("{domain}/workflows/{workflow}/instances/{instance}/transitions/sysGetView")]
    public async Task<IActionResult> GetAvailableSysGetViewAsync(
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromRoute] string instance,
        [FromQuery] string? version,
        CancellationToken cancellationToken = default)
    {
        var input = new GetAvailableSysGetViewInput
        {
            Domain = domain,
            Workflow = workflow,
            Instance = instance,
            Version = version
        };

        var response = await queryAppService.GetAvailableSysGetViewAsync(input, cancellationToken);
        return Ok(response.Data);
    }
    
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("{domain}/workflows/{workflow}/instances/{instance}/transitions/items")]
    public async Task<IActionResult> GetTransitionItemsAsync(
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromRoute] string instance,
        [FromQuery] string? version,
        CancellationToken cancellationToken = default)
    {
        var input = new GetTransitionItemsInput
        {
            Domain = domain,
            Workflow = workflow,
            Instance = instance,
            Version = version
        };

        var response = await queryAppService.GetTransitionItemsAsync(input, cancellationToken);
        return Ok(response.Data);
    }
    
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("{domain}/workflows/{workflow}/instances/{instance}/correlations/active")]
    public async Task<IActionResult> GetActiveCorrelationsAsync(
        [FromRoute] string domain,
        [FromRoute] string workflow,
        [FromRoute] string instance,
        [FromQuery] string? version,
        CancellationToken cancellationToken = default)
    {
        var input = new GetActiveCorrelationsInput
        {
            Domain = domain,
            Workflow = workflow,
            Instance = instance,
            Version = version
        };

        var response = await queryAppService.GetActiveCorrelationsAsync(input, cancellationToken);
        return Ok(response.Data);
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