using Microsoft.AspNetCore.Mvc;
using BBT.Workflow.Tasks;

namespace BBT.Workflow.Execution.Controllers.Executions;

/// <summary>
/// Controller for handling direct task execution requests from the Orchestration service via Dapr Service Invocation.
/// This controller provides endpoints for executing individual workflow tasks without orchestration logic.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/execution")]
public sealed class ExecutionController(
    ITaskCommandAppService taskCommandAppService,
    ILogger<ExecutionController> logger)
    : ControllerBase
{
    /// <summary>
    /// Executes a single workflow task directly without orchestration logic.
    /// Returns context updates that occurred during task execution for synchronization
    /// with the orchestration service.
    /// </summary>
    /// <param name="input">The task execution request.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task execution result with context updates.</returns>
    [HttpPost("task")]
    public async Task<IActionResult> ExecuteTaskAsync(
        [FromBody] TaskExecutionRequestInput input,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Executing task {TaskKey} for instance {InstanceId}", 
                input.OnExecuteTask.Task.Key, input.Context.InstanceId);

            // Execute task and capture context updates for synchronization
            var contextUpdate = await taskCommandAppService.ExecuteTaskAsync(
                input,
                cancellationToken);
            
            logger.LogInformation("Successfully executed task {TaskKey} for instance {InstanceId}. Context updates: TaskResponse={TaskResponseCount}, InstanceData={InstanceDataCount}", 
                input.OnExecuteTask.Task.Key, 
                input.Context.InstanceId,
                contextUpdate.TaskResponse.Count,
                contextUpdate.InstanceDataUpdates.Count);
            
            return Ok(new TaskExecutionResponseOutput 
            { 
                Success = true, 
                Message = "Task executed successfully",
                ContextUpdate = contextUpdate
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing task for instance {InstanceId}", 
                input.Context.InstanceId);
            
            return BadRequest(new TaskExecutionResponseOutput 
            { 
                Success = false, 
                Message = ex.Message 
            });
        }
    }
} 