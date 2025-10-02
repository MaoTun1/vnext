using BBT.Workflow.Definitions;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Instances;
using BBT.Workflow.States;
using BBT.Workflow.Scripting;
using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkflowExecutionContext = BBT.Workflow.Shared.ExecutionContext;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Service implementation for handling automatic workflow transitions.
/// Orchestrates the sequential execution of automatic transitions without creating circular dependencies.
/// Executes transitions one by one to prevent state conflicts and stops after the first successful transition.
/// </summary>
public sealed class AutoTransitionService(
    IStateMachineService stateMachineService,
    IServiceProvider serviceProvider,
    IInstanceRefreshStrategy instanceRefreshStrategy,
    ITimerExecutionService timerExecutionService,
    IBackgroundJobService backgroundJobService,
    ILogger<AutoTransitionService> logger) : IAutoTransitionService
{
    /// <inheritdoc />
    /// <remarks>
    /// This implementation processes automatic transitions sequentially to avoid race conditions
    /// when multiple transitions might try to modify the instance state simultaneously.
    /// The method stops execution after the first successful transition to maintain state consistency.
    /// </remarks>
    public async Task CheckAndExecuteAutomaticTransitionsAsync(
        Definitions.Workflow workflow,
        Instance instance,
        CancellationToken cancellationToken = default)
    {
        var autoTransitions = stateMachineService.GetAutomaticTransitions(workflow, instance);
        var transitions = autoTransitions as Transition[] ?? autoTransitions.ToArray();
        
        if (!transitions.Any())
        {
            return;
        }

        var input = new TransitionInput(
            workflow.Domain,
            workflow.Key,
            workflow.Version,
            data: null,
            true)
        {
            ExecutionContext = WorkflowExecutionContext.System
        };

        // Execute transitions sequentially to avoid state conflicts
        // Only the first successful transition should be executed
        bool anySuccess = false;

        foreach (var transition in transitions)
        {
            try
            {
                logger.LogDebug(
                    "Attempting AutoTransition. InstanceId={InstanceId}, Transition={TransitionKey}",
                    instance.Id, transition.Key);

                // Use service locator pattern to resolve WorkflowExecutionService at runtime
                // This avoids circular dependency since we're not injecting it in constructor
                using var scope = serviceProvider.CreateScope();
                var workflowExecutionService = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionService>();

                await workflowExecutionService.ExecuteTransitionAsync(
                    instance.Id,
                    transition.Key,
                    input,
                    cancellationToken);

                // If we reach here, the transition was successful
                anySuccess = true;

                logger.LogInformation(
                    "AutoTransition succeeded. InstanceId={InstanceId}, Transition={TransitionKey}",
                    instance.Id, transition.Key);

                // Stop processing after first successful transition
                break;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Operation was cancelled, re-throw to propagate cancellation
                throw;
            }
            catch (TransitionRuleFailedException ex)
            {
                logger.LogWarning(ex,
                    "AutoTransition transition rule failed. InstanceId={InstanceId}, Transition={TransitionKey}. Continuing to next transition.",
                    instance.Id, transition.Key);
                // Continue to next transition
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "AutoTransition failed. InstanceId={InstanceId}, Transition={TransitionKey}. Trying next transition.",
                    instance.Id, transition.Key);
                throw;
            }
        }

        if (!anySuccess)
        {
            throw new AutoTransitionFailedException(instance.Id, workflow.Key);
        }
    }

    /// <inheritdoc />
    public async Task<AutoTransitionResult> CheckAndExecuteAutomaticTransitionsWithResultAsync(
        Definitions.Workflow workflow,
        Instance instance,
        CancellationToken cancellationToken = default)
    {
        var autoTransitions = stateMachineService.GetAutomaticTransitions(workflow, instance);
        var transitions = autoTransitions as Transition[] ?? autoTransitions.ToArray();
        
        if (!transitions.Any())
        {
            return AutoTransitionResult.NoTransitionsExecuted(instance);
        }

        var input = new TransitionInput(
            workflow.Domain,
            workflow.Key,
            workflow.Version,
            data: null,
            true)
        {
            ExecutionContext = WorkflowExecutionContext.System
        };

        // Execute transitions sequentially to avoid state conflicts
        // Only the first successful transition should be executed
        bool anySuccess = false;

        foreach (var transition in transitions)
        {
            try
            {
                logger.LogDebug(
                    "Attempting AutoTransition. InstanceId={InstanceId}, Transition={TransitionKey}",
                    instance.Id, transition.Key);

                // Use service locator pattern to resolve WorkflowExecutionService at runtime
                // This avoids circular dependency since we're not injecting it in constructor
                using var scope = serviceProvider.CreateScope();
                var workflowExecutionService = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionService>();

                await workflowExecutionService.ExecuteTransitionAsync(
                    instance.Id,
                    transition.Key,
                    input,
                    cancellationToken);

                // If we reach here, the transition was successful
                anySuccess = true;

                logger.LogInformation(
                    "AutoTransition succeeded. InstanceId={InstanceId}, Transition={TransitionKey}",
                    instance.Id, transition.Key);

                // Stop processing after first successful transition
                break;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Operation was cancelled, re-throw to propagate cancellation
                throw;
            }
            catch (TransitionRuleFailedException ex)
            {
                logger.LogWarning(ex,
                    "AutoTransition transition rule failed. InstanceId={InstanceId}, Transition={TransitionKey}. Continuing to next transition.",
                    instance.Id, transition.Key);
                // Continue to next transition
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "AutoTransition failed. InstanceId={InstanceId}, Transition={TransitionKey}. Trying next transition.",
                    instance.Id, transition.Key);
                throw;
            }
        }

        if (!anySuccess)
        {
            throw new AutoTransitionFailedException(instance.Id, workflow.Key);
        }

        // Refresh the instance after successful auto transitions
        var refreshedInstance = await instanceRefreshStrategy.RefreshIfNeededAsync(
            instance,
            afterAutoTransition: true,
            cancellationToken);

        return AutoTransitionResult.TransitionsExecuted(refreshedInstance);
    }

    /// <inheritdoc />
    public async Task<AutoTransitionResult> ExecuteAutomaticAndScheduledTransitionsAsync(
        Definitions.Workflow workflow,
        Instance instance,
        ScriptContext scriptContext,
        CancellationToken cancellationToken = default)
    {
        // First, execute automatic transitions and get the result
        var autoTransitionResult = await CheckAndExecuteAutomaticTransitionsWithResultAsync(
            workflow,
            instance,
            cancellationToken);

        // Use the refreshed instance from auto transitions (or original if none executed)
        var currentInstance = autoTransitionResult.RefreshedInstance ?? instance;

        // If instance is completed, no need to schedule transitions
        if (currentInstance.IsCompleted)
        {
            return autoTransitionResult;
        }

        // Schedule transitions for later execution
        await ScheduleTransitionsForLaterExecutionAsync(
            workflow,
            currentInstance,
            scriptContext,
            cancellationToken);

        return autoTransitionResult;
    }

    /// <summary>
    /// Schedules workflow transitions for later execution based on their timing configurations.
    /// This method identifies transitions that are configured to be executed at specific times or delays
    /// and enqueues them as background jobs for future execution.
    /// </summary>
    /// <param name="workflow">The workflow definition containing transitions that may need to be scheduled.</param>
    /// <param name="instance">The workflow instance for which transitions will be scheduled.</param>
    /// <param name="scriptContext">The script context for timer execution.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Task representing the scheduling operation.</returns>
    private async Task ScheduleTransitionsForLaterExecutionAsync(
        Definitions.Workflow workflow,
        Instance instance,
        ScriptContext scriptContext,
        CancellationToken cancellationToken = default)
    {
        var scheduledTransitions = stateMachineService.GetScheduledTransitions(workflow, instance);
        var transitions = scheduledTransitions as Transition[] ?? scheduledTransitions.ToArray();
        
        if (!transitions.Any())
        {
            return;
        }

        var tasks = transitions
            .Where(t => t.Timer != null)
            .Select(async transition =>
            {
                var timerSchedule = await timerExecutionService.ExecuteRuleAsync(
#pragma warning disable CS8604 // Possible null reference argument.
                    transition.Timer, scriptContext, cancellationToken);
#pragma warning restore CS8604 // Possible null reference argument.

                await backgroundJobService.EnqueueTransitionTimerAsync(
                    instance.Id,
                    workflow.Key,
                    workflow.Domain,
                    workflow.Version,
                    transition.Key,
                    timerSchedule,
                    cancellationToken);
            });

        await Task.WhenAll(tasks);
    }
}
