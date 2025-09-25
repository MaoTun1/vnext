using BBT.Workflow.Definitions;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Execution.StateMachine;
using BBT.Workflow.Instances;
using BBT.Workflow.States;
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
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "AutoTransition failed. InstanceId={InstanceId}, Transition={TransitionKey}. Trying next transition.",
                    instance.Id, transition.Key);

                // Continue to next transition
                continue;
            }
        }

        if (!anySuccess)
        {
            throw new AutoTransitionFailedException(instance.Id, workflow.Key);
        }
    }
}
