using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes trigger transition workflow tasks that trigger transitions on workflow instances.
/// This executor delegates to appropriate strategies based on trigger type (Start, Trigger, SubProcess).
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="runtimeInfoProvider">The runtime information provider for domain checks.</param>
/// <param name="strategyFactory">Factory for creating trigger transition strategies.</param>
/// <param name="logger">The logger instance for logging trigger transition task execution details.</param>
public sealed class TriggerTransitionTaskExecutor(
    IScriptEngine scriptEngine,
    IRuntimeInfoProvider runtimeInfoProvider,
    ITriggerTransitionStrategyFactory strategyFactory,
    ILogger<TriggerTransitionTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a trigger transition task by preparing the request through script mapping, 
    /// delegating to the appropriate strategy, and processing the response through output mapping.
    /// </summary>
    /// <param name="task">The trigger transition workflow task containing transition configuration.</param>
    /// <param name="scriptCode">The script code that handles input preparation and output processing.</param>
    /// <param name="context">The script context containing instance data and headers.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed response data
    /// from the transition execution after output mapping transformation.
    /// </returns>
    /// <exception cref="InvalidCastException">Thrown when the task cannot be cast to TriggerTransitionTask type.</exception>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var triggerTask = (task as TriggerTransitionTask)!;

        Logger.LogInformation("Starting trigger transition task execution for task {TaskKey} - TriggerType: {TriggerType}, Domain: {Domain}, Flow: {Flow}",
            triggerTask.Key, triggerTask.TriggerType, triggerTask.TriggerDomain, triggerTask.TriggerKey);

        try
        {
            // Check runtime domain
            runtimeInfoProvider.Check(context.Runtime.Domain);

            Logger.LogDebug("Preparing input for trigger transition task {TaskKey}", triggerTask.Key);
            await PrepareInputAsync(triggerTask, scriptCode, context, cancellationToken);

            // Get appropriate strategy and execute
            var strategy = strategyFactory.Get(triggerTask.TriggerType);
            await strategy.ExecuteAsync(triggerTask, context, cancellationToken);

            Logger.LogDebug("Processing output for trigger transition task {TaskKey}", triggerTask.Key);
            var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);

            Logger.LogInformation("Trigger transition task {TaskKey} execution completed, returning processed output", triggerTask.Key);
            return outputResponse;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred during trigger transition task {TaskKey} execution - TriggerType: {TriggerType}, Domain: {Domain}, Flow: {Flow}",
                triggerTask.Key, triggerTask.TriggerType, triggerTask.TriggerDomain, triggerTask.TriggerFlow);

            StandardTaskResponse standardResponse = CreateErrorResponse(
                errorMessage: ex.Message,
                taskType: nameof(TaskType.TriggerTransition),
                executionDurationMs: 0,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["TaskKey"] = triggerTask.Key,
                    ["TriggerType"] = triggerTask.TriggerType.ToString(),
                    ["Domain"] = triggerTask.TriggerDomain,
                    ["Flow"] = triggerTask.TriggerFlow
                });
            context.SetStandardResponse(standardResponse);
            throw;
        }
    }

}

