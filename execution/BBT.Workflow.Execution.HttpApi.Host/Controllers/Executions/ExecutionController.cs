using BBT.Workflow.Domain;
using BBT.Workflow.HttpApi.Shared;
using BBT.Workflow.Tasks;
using BBT.Workflow.Telemetry;
using Microsoft.AspNetCore.Mvc;

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
    /// <response code="200">Task executed successfully</response>
    /// <response code="400">Validation error or business rule violation</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("task")]
    [ProducesResponseType(typeof(TaskExecutionResponseOutput), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExecuteTaskAsync(
        [FromBody] TaskExecutionRequestInput input,
        CancellationToken cancellationToken = default)
    {
        // Enrich all logs within this scope with comprehensive workflow context for distributed tracing
        // Note: Task type is not available at controller level, will be added by LocalTaskExecutor
        using (logger.BeginScope(new Dictionary<string, object>
        {
            [TelemetryConstants.ScopeFields.Domain] = input.Context.Workflow.Domain,
            [TelemetryConstants.ScopeFields.Flow] = input.Context.Workflow.Key,
            [TelemetryConstants.ScopeFields.FlowVersion] = input.Context.Workflow.Version,
            [TelemetryConstants.ScopeFields.InstanceId] = input.Context.InstanceId,
            [TelemetryConstants.ScopeFields.TransitionKey] = input.Context.TransitionKey ?? "N/A",
            [TelemetryConstants.ScopeFields.TaskKey] = input.OnExecuteTask.Task.Key,
            [TelemetryConstants.ScopeFields.TaskTrigger] = input.TaskTrigger.ToString()
        }))
        {
            logger.LogInformation("Executing task {TaskKey} for instance {InstanceId}", 
                input.OnExecuteTask.Task.Key, input.Context.InstanceId);

            // Execute task and capture context updates for synchronization
            var result = await taskCommandAppService.ExecuteTaskAsync(input, cancellationToken);
            
            // Use Result Pattern to automatically handle errors
            if (!result.IsSuccess)
                return new ObjectResult(result.ToProblemDetails()) 
                { 
                    StatusCode = result.ToProblemDetails().Status 
                };
            
            logger.LogInformation("Successfully executed task {TaskKey} for instance {InstanceId}. Context updates: TaskResponse={TaskResponseCount}, InstanceData={InstanceDataCount}", 
                input.OnExecuteTask.Task.Key, 
                input.Context.InstanceId,
                result.Value!.TaskResponse.Count,
                result.Value!.InstanceDataUpdates.Count);
            
            return Ok(new TaskExecutionResponseOutput 
            { 
                Success = true, 
                Message = "Task executed successfully",
                ContextUpdate = result.Value!
            });
        }
    }
} 