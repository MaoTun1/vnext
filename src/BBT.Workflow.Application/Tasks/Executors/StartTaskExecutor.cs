using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes start tasks that create new workflow instances.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="taskExecutorFactory">Factory for creating task executors.</param>
/// <param name="httpTaskFactory">Factory for creating HTTP tasks.</param>
/// <param name="logger">The logger instance for logging start task execution details.</param>
public sealed class StartTaskExecutor(
    IScriptEngine scriptEngine,
    ITaskExecutorFactory taskExecutorFactory,
    ITriggerTransitionHttpTaskFactory httpTaskFactory,
    ILogger<StartTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a start task by preparing the request through script mapping,
    /// creating an HTTP task to start a new workflow instance, and processing the response.
    /// </summary>
    /// <param name="task">The start workflow task containing start configuration.</param>
    /// <param name="scriptCode">The script code that handles input preparation and output processing.</param>
    /// <param name="context">The script context containing instance data and headers.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed response data
    /// from the start execution after output mapping transformation.
    /// </returns>
    /// <exception cref="InvalidCastException">Thrown when the task cannot be cast to StartTask type.</exception>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var startTask = (task as StartTask)!;
        try
        {
            await PrepareInputAsync(startTask, scriptCode, context, cancellationToken);

            // Execute start logic
            var executionResult = await ExecuteStartAsync(startTask, context, cancellationToken);
            
            // If execution failed, throw exception to maintain existing error handling behavior
            if (!executionResult.IsSuccess)
            {
                Logger.LogError("Start execution failed for task {TaskKey}: {ErrorCode} - {ErrorMessage}",
                    startTask.Key, executionResult.Error.Code, executionResult.Error.Message);
                throw new InvalidOperationException($"Start execution failed: {executionResult.Error.Message}");
            }
            
            var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);
            return outputResponse;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred during start task {TaskKey} execution - Domain: {Domain}, Flow: {Flow}",
                startTask.Key, startTask.TriggerDomain, startTask.TriggerFlow);

            StandardTaskResponse standardResponse = CreateErrorResponse(
                errorMessage: ex.Message,
                taskType: nameof(TaskType.StartTrigger),
                executionDurationMs: 0,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["TaskKey"] = startTask.Key,
                    ["Domain"] = startTask.TriggerDomain,
                    ["Flow"] = startTask.TriggerFlow
                });
            context.SetStandardResponse(standardResponse);
            throw;
        }
    }

    /// <summary>
    /// Executes the start logic by creating a new workflow instance using HttpTask.
    /// </summary>
    private async Task<Result> ExecuteStartAsync(
        StartTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                // Create path using InstanceUrlTemplates.Start format
                var path = InstanceUrlTemplates.Start(
                    task.TriggerDomain,
                    task.TriggerFlow);
                
                var httpTaskResult = httpTaskFactory.CreateHttpTask(task, context, path, "POST");
                if (!httpTaskResult.IsSuccess)
                {
                    Logger.LogError("Failed to create HTTP task for Start trigger: {Error}", httpTaskResult.Error.Code);
                    throw new InvalidOperationException($"Failed to create HTTP task: {httpTaskResult.Error.Message}");
                }

                var httpTask = httpTaskResult.Value!;

                var httpExecutor = taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

                if (httpExecutor == null)
                    throw new InvalidOperationException("HttpTaskExecutor not found");
                
                await httpExecutor.CallAsync(httpTask, context, ct);
            },
            cancellationToken,
            ex => Error.Failure(WorkflowErrorCodes.TriggerStartExecutionFailed, 
                $"Failed to execute Start trigger for task '{task.Key}': {ex.Message}"));
    }
}

