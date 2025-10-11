using BBT.Aether.Guids;
using BBT.Workflow.Instances;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that creates and persists the transition record.
/// This step tracks the transition attempt and provides audit trail.
/// </summary>
public sealed class CreateTransitionRecordStep(
    IInstanceTransitionRepository instanceTransitionRepository,
    IGuidGenerator guidGenerator,
    ILogger<CreateTransitionRecordStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.CreateTransition;

    /// <inheritdoc />
    public async Task<StepOutcome> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogDebug("Creating transition record for {TransitionKey} on instance {InstanceId}",
            context.TransitionKey, context.InstanceId);

        var transitionKey = context.Items.TryGetValue("NextTransitionKey", out var v) && v is string next && !string.IsNullOrEmpty(next)
            ? next : context.TransitionKey;
        
        // Create the transition record
        var instanceTransition = InstanceTransition.Create(
            guidGenerator.Create(),
            context.InstanceId,
            transitionKey,
            context.Instance.GetCurrentState,
            new JsonData(context.Data),
            new JsonData(JsonSerializer.Serialize(context.Headers))
        );

        // Persist the record
        await instanceTransitionRepository.InsertAsync(instanceTransition, saveChanges: true, cancellationToken);

        // Store in context for other steps to use
        context.Items["TransitionRecordId"] = instanceTransition.Id;
        context.Items.Remove("NextTransitionKey");

        logger.LogDebug("Created transition record {TransitionId} for {TransitionKey}",
            instanceTransition.Id, context.TransitionKey);
        
        return StepOutcome.Continue();
    }
}
