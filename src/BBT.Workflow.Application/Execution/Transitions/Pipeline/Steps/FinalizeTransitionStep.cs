using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Workflow.Instances;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Scripting;
using BBT.Workflow.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that finalizes the transition execution.
/// Updates the transition record and performs cleanup operations.
/// Uses Result pattern for exception-free error handling.
/// </summary>
public sealed class FinalizeTransitionStep(
    IInstanceTransitionRepository instanceTransitionRepository,
    IWorkflowMetrics workflowMetrics) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Finalize;

    /// <inheritdoc />
    [Log]
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(FinalizeTransitionStep)}");
        
        // Railway Oriented Programming: Chain operations, each wrapped in Try
        return await GetTransitionRecordId(context)
            .ThenAsync(recordId => LoadTransitionRecord(recordId, cancellationToken))
            .ThenAsync(transition => MarkTransitionAsCompleted(context, transition))
            .OnSuccess(transition => RecordStateDurationMetric(context, transition))
            .ThenAsync(transition => UpdateTransitionRecord(transition, cancellationToken))
            .ThenAsync(_ => PerformCleanupAndFinalize(context));
    }

    /// <summary>
    /// Gets the transition record ID from context items.
    /// </summary>
    private Task<Result<Guid>> GetTransitionRecordId(TransitionExecutionContext context)
    {
        if (context.Items.TryGetValue("TransitionRecordId", out var record) && record is Guid recordId)
        {
            return Task.FromResult(Result<Guid>.Ok(recordId));
        }
        
        // Return a special Guid to indicate no record (will be handled in next step)
        return Task.FromResult(Result<Guid>.Ok(Guid.Empty));
    }

    /// <summary>
    /// Loads the transition record from repository.
    /// </summary>
    private async Task<Result<InstanceTransition?>> LoadTransitionRecord(Guid recordId, CancellationToken cancellationToken)
    {
        // Skip if no record ID
        if (recordId == Guid.Empty)
        {
            return Result<InstanceTransition?>.Ok(null);
        }

        var loadResult = await ResultExtensions.TryAsync(
            async ct => await instanceTransitionRepository.GetAsync(recordId, true, ct),
            cancellationToken);

        return loadResult.IsSuccess
            ? Result<InstanceTransition?>.Ok(loadResult.Value)
            : Result<InstanceTransition?>.Fail(loadResult.Error);
    }

    /// <summary>
    /// Marks the transition as completed.
    /// </summary>
    private Task<Result<InstanceTransition?>> MarkTransitionAsCompleted(
        TransitionExecutionContext context,
        InstanceTransition? transition)
    {
        if (transition == null)
        {
            return Task.FromResult(Result<InstanceTransition?>.Ok(null));
        }
        
        transition.Completed(context.Instance.GetCurrentState);
        return Task.FromResult(Result<InstanceTransition?>.Ok(transition));
    }

    /// <summary>
    /// Records state duration metric if available.
    /// </summary>
    private void RecordStateDurationMetric(TransitionExecutionContext context, InstanceTransition? transition)
    {
        if (transition?.Duration.HasValue == true)
        {
            workflowMetrics.RecordStateDuration(
                context.Workflow.Key,
                transition.FromState,
                transition.Duration.Value.TotalSeconds);
        }
    }

    /// <summary>
    /// Updates the transition record in repository.
    /// </summary>
    private async Task<Result<InstanceTransition?>> UpdateTransitionRecord(
        InstanceTransition? transition,
        CancellationToken cancellationToken)
    {
        if (transition == null)
        {
            return Result<InstanceTransition?>.Ok(null);
        }

        var updateResult = await ResultExtensions.TryAsync(
            async ct => await instanceTransitionRepository.UpdateCompletedAsync(transition, ct),
            cancellationToken);

        return updateResult.IsSuccess
            ? Result<InstanceTransition?>.Ok(transition)
            : Result<InstanceTransition?>.Fail(updateResult.Error);
    }

    /// <summary>
    /// Performs cleanup operations and finalizes the step execution.
    /// </summary>
    private Task<Result<StepOutcome>> PerformCleanupAndFinalize(
        TransitionExecutionContext context)
    {
        return Task.FromResult(
            ResultExtensions.Try(() =>
            {
                // Dispose ScriptContext if it exists
                if (context.Cache.TryGetValue("ScriptContext", out var scriptContextObj) &&
                    scriptContextObj is ScriptContext scriptContext)
                {
                    scriptContext.Dispose();
                }

                context.ClearCacheForFinalize();
                return StepOutcome.Continue();
            })
        );
    }
}
