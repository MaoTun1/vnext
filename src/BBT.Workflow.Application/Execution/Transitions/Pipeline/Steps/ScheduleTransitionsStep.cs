using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.ReEntry;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that schedules future transitions based on timers.
/// Enqueues scheduled transitions for later execution.
/// </summary>
public sealed class ScheduleTransitionsStep(
    IReentryDispatcher reentryDispatcher,
    IScriptContextFactory scriptContextFactory,
    ILogger<ScheduleTransitionsStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Schedule;

    /// <inheritdoc />
    public async Task ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Target?.ScheduledTransitions == null || !context.Target.ScheduledTransitions.Any())
        {
            logger.LogTrace("No scheduled transitions for state {StateName}", context.Target?.Key ?? "Unknown");
            return;
        }

        logger.LogDebug("Scheduling {Count} transitions for state {StateName} on instance {InstanceId}",
            context.Target.ScheduledTransitions.Count(), context.Target.Key, context.InstanceId);

        // Get or build script context for timer evaluation
        var scriptContext = context.GetOrBuildScriptContext(() => 
            CreateScriptContext(context));

        foreach (var scheduledTransition in context.Target.ScheduledTransitions)
        {
            try
            {
                await ScheduleTransitionAsync(context, scheduledTransition, scriptContext, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to schedule transition {TransitionKey} for instance {InstanceId}",
                    scheduledTransition.Key, context.InstanceId);
                // Continue with other scheduled transitions
            }
        }

        logger.LogDebug("Completed scheduling transitions for state {StateName}", context.Target.Key);
    }

    /// <summary>
    /// Schedules a single transition for future execution.
    /// </summary>
    private async Task ScheduleTransitionAsync(
        TransitionExecutionContext context,
        Transition scheduledTransition,
        ScriptContext scriptContext,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("Scheduling transition {TransitionKey} for instance {InstanceId}",
            scheduledTransition.Key, context.InstanceId);

        // TODO: Evaluate timer to get the schedule
        // This would involve:
        // 1. Evaluating the timer expression using the script context
        // 2. Converting the result to a TimerSchedule
        // 3. Creating the re-entry command with proper scheduling information
        
        // For now, we'll create a basic re-entry command
        var command = ReentryCommand.ForScheduled(
            context.InstanceId,
            context.Domain,
            context.WorkflowKey,
            scheduledTransition.Key,
            context.ExecutionChainId,
            context.ChainDepth,
            context.Headers);

        // Dispatch for scheduling
        await reentryDispatcher.DispatchScheduledAsync(command, cancellationToken);

        logger.LogTrace("Scheduled transition {TransitionKey} for instance {InstanceId}",
            scheduledTransition.Key, context.InstanceId);
    }

    /// <summary>
    /// Creates a script context for timer evaluation.
    /// </summary>
    private ScriptContext CreateScriptContext(TransitionExecutionContext context)
    {
        return scriptContextFactory.NewBuilder()
            .WithWorkflow(context.Workflow)
            .WithInstance(context.Instance)
            .WithTransition(context.Transition)
            .WithBody(context.Data)
            .WithHeaders(context.Headers.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value))
            .BuildAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}
