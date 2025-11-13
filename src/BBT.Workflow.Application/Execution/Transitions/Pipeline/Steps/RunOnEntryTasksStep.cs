using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that executes the target state's OnEntry tasks.
/// These tasks run when entering the new state.
/// Uses Result pattern for exception-free error handling.
/// </summary>
public sealed class RunOnEntryTasksStep(
    ITaskOrchestrationService taskOrchestrationService,
    IScriptContextFactory scriptContextFactory,
    IInstanceRepository instanceRepository,
    ILogger<RunOnEntryTasksStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.OnEntry;

    /// <inheritdoc />
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Target?.OnEntries == null || !context.Target.OnEntries.Any())
        {
            logger.LogTrace("No OnEntry tasks for state {StateName}", context.Target?.Key ?? "Unknown");
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        logger.LogDebug("Executing OnEntry tasks for state {StateName} on instance {InstanceId}",
            context.Target.Key, context.InstanceId);

        // Use ResultExtensions.TryAsync to wrap potentially throwing operations
        return await ResultExtensions.TryAsync<StepOutcome>(async ct =>
        {
            // Get or build script context
            var scriptContext = context.GetOrBuildScriptContext(() => 
                CreateScriptContext(context));

            // Get the transition record from previous step
            var instanceTransitionId = context.Items.TryGetValue("TransitionRecordId", out var record) 
                ? record as Guid?
                : null;

            // Execute the tasks
            await taskOrchestrationService.ExecuteAsync(
                context.Target.OnEntries,
                instanceTransitionId,
                TaskTrigger.OnEntry,
                scriptContext,
                ct);

            context.ApplyScriptContextChanges(scriptContext);

            await instanceRepository.UpdateAsync(context.Instance, true, ct);
            
            logger.LogDebug("Completed OnEntry tasks for state {StateName}", context.Target.Key);
            return StepOutcome.Continue();
        }, 
        cancellationToken,
        ex => Error.Failure(
            WorkflowErrorCodes.ExecutionStepFailed, 
            $"OnEntry tasks execution failed: {ex.Message}", 
            ex.GetType().Name));
    }

    /// <summary>
    /// Creates a script context for task execution.
    /// </summary>
    private ScriptContext CreateScriptContext(TransitionExecutionContext context)
    {
        var builder = scriptContextFactory.NewBuilder()
            .WithWorkflow(context.Workflow)
            .WithInstance(context.Instance)
            .WithBody(context.Data)
            .WithHeaders(context.Headers.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value));
        
        if (context.Transition != null)
            builder.WithTransition(context.Transition);
        
        return builder.BuildAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}
