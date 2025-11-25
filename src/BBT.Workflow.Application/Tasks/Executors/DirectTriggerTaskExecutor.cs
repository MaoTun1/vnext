using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes direct trigger tasks that trigger transitions on workflow instances.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="runtimeInfoProvider">The runtime information provider for domain checks.</param>
/// <param name="taskExecutorFactory">Factory for creating task executors.</param>
/// <param name="httpTaskFactory">Factory for creating HTTP tasks.</param>
/// <param name="logger">The logger instance for logging direct trigger task execution details.</param>
public sealed class DirectTriggerTaskExecutor(
    IScriptEngine scriptEngine,
    IRuntimeInfoProvider runtimeInfoProvider,
    ITaskExecutorFactory taskExecutorFactory,
    ITriggerTransitionHttpTaskFactory httpTaskFactory,
    ILogger<DirectTriggerTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a direct trigger task by preparing the request through script mapping,
    /// creating an HTTP task to trigger a transition, and processing the response.
    /// </summary>
    /// <param name="task">The direct trigger workflow task containing trigger configuration.</param>
    /// <param name="scriptCode">The script code that handles input preparation and output processing.</param>
    /// <param name="context">The script context containing instance data and headers.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed response data
    /// from the trigger execution after output mapping transformation.
    /// </returns>
    /// <exception cref="InvalidCastException">Thrown when the task cannot be cast to DirectTriggerTask type.</exception>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var directTriggerTask = (task as DirectTriggerTask)!;

        Logger.LogInformation("Starting direct trigger task execution for task {TaskKey} - Domain: {Domain}, Flow: {Flow}, Transition: {Transition}",
            directTriggerTask.Key, directTriggerTask.TriggerDomain, directTriggerTask.TriggerFlow, directTriggerTask.TransitionName);

        try
        {
            // Check runtime domain
            runtimeInfoProvider.Check(context.Runtime.Domain);

            Logger.LogDebug("Preparing input for direct trigger task {TaskKey}", directTriggerTask.Key);
            await PrepareInputAsync(directTriggerTask, scriptCode, context, cancellationToken);

            // Execute direct trigger logic
            var executionResult = await ExecuteDirectTriggerAsync(directTriggerTask, context, cancellationToken);
            
            // If execution failed, throw exception to maintain existing error handling behavior
            if (!executionResult.IsSuccess)
            {
                Logger.LogError("Direct trigger execution failed for task {TaskKey}: {ErrorCode} - {ErrorMessage}",
                    directTriggerTask.Key, executionResult.Error.Code, executionResult.Error.Message);
                throw new InvalidOperationException($"Direct trigger execution failed: {executionResult.Error.Message}");
            }

            Logger.LogDebug("Processing output for direct trigger task {TaskKey}", directTriggerTask.Key);
            var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);

            Logger.LogInformation("Direct trigger task {TaskKey} execution completed, returning processed output", directTriggerTask.Key);
            return outputResponse;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred during direct trigger task {TaskKey} execution - Domain: {Domain}, Flow: {Flow}, Transition: {Transition}",
                directTriggerTask.Key, directTriggerTask.TriggerDomain, directTriggerTask.TriggerFlow, directTriggerTask.TransitionName);

            StandardTaskResponse standardResponse = CreateErrorResponse(
                errorMessage: ex.Message,
                taskType: nameof(TaskType.DirectTrigger),
                executionDurationMs: 0,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["TaskKey"] = directTriggerTask.Key,
                    ["Domain"] = directTriggerTask.TriggerDomain,
                    ["Flow"] = directTriggerTask.TriggerFlow,
                    ["TransitionName"] = directTriggerTask.TransitionName ?? ""
                });
            context.SetStandardResponse(standardResponse);
            throw;
        }
    }

    /// <summary>
    /// Executes the direct trigger logic by triggering a transition on a target instance.
    /// </summary>
    private async Task<Result> ExecuteDirectTriggerAsync(
        DirectTriggerTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                // Validate TransitionName
                if (string.IsNullOrWhiteSpace(task.TransitionName))
                {
                    throw new InvalidOperationException("TransitionName is required for Direct trigger type");
                }

                Logger.LogInformation("Handling Direct trigger for task {TaskKey} - InstanceId: {InstanceId}, Transition: {Transition}",
                    task.Key, context.Instance.Id, task.TransitionName);

                // Resolve instance ID using the factory's ResolveInstanceIdAsync method
                var instanceIdResult = await httpTaskFactory.ResolveInstanceIdAsync(task, context, ct);
                if (!instanceIdResult.IsSuccess)
                {
                    Logger.LogError("Failed to resolve instance ID for Direct trigger: {Error}", instanceIdResult.Error.Code);
                    throw new InvalidOperationException($"Failed to resolve instance ID: {instanceIdResult.Error.Message}");
                }

                var instanceId = instanceIdResult.Value!;

                // Create path using InstanceUrlTemplates.Transition format
                var path = string.Format(InstanceUrlTemplates.Transition,
                    task.TriggerDomain,
                    task.TriggerFlow,
                    instanceId,
                    task.TransitionName);

                var httpTaskResult = httpTaskFactory.CreateHttpTask(task, context, path, "PATCH");
                if (!httpTaskResult.IsSuccess)
                {
                    Logger.LogError("Failed to create HTTP task for Direct trigger: {Error}", httpTaskResult.Error.Code);
                    throw new InvalidOperationException($"Failed to create HTTP task: {httpTaskResult.Error.Message}");
                }

                var httpTask = httpTaskResult.Value!;

                var httpExecutor = taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

                if (httpExecutor == null)
                    throw new InvalidOperationException("HttpTaskExecutor not found");

                Logger.LogDebug("Calling HttpTaskExecutor.CallAsync for Direct trigger task {TaskKey}", task.Key);
                await httpExecutor.CallAsync(httpTask, context, ct);
            },
            cancellationToken,
            ex => Error.Failure(WorkflowErrorCodes.TriggerDirectExecutionFailed, 
                $"Failed to execute Direct trigger for task '{task.Key}': {ex.Message}"));
    }
}

