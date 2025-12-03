using System.Diagnostics;
using BBT.Aether.Guids;
using BBT.Workflow.Execution.Transitions.Services;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using System.Text.Json;
using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Workflow.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that creates and persists the transition record.
/// This step tracks the transition attempt and provides audit trail.
/// </summary>
public sealed class CreateTransitionRecordStep(
    IInstanceTransitionRepository instanceTransitionRepository,
    IInstanceRepository instanceRepository,
    IGuidGenerator guidGenerator,
    ITransitionDataMapper transitionDataMapper,
    IRuntimeInfoProvider runtimeInfoProvider) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.CreateTransition;

    /// <inheritdoc />
    [Log]
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(CreateTransitionRecordStep)}");

        // Skip for SubFlow resume - transition record already exists
        if (context.Directives.IsSubFlowResume)
        {
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        // Build transition info
        var transitionKey = GetTransitionKey(context);
        var (instanceTransition, transition) = CreateInstanceTransition(context, transitionKey);

        // Railway chain: Map data -> Add to instance -> Persist
        return await MapTransitionDataAsync(context, transition, cancellationToken)
            .Tap(mappedData => AddMappedDataToInstance(context, mappedData, transition))
            .TapAsync(_ => instanceRepository.UpdateAsync(context.Instance, true, cancellationToken))
            .TapAsync(_ => instanceTransitionRepository.InsertAsync(instanceTransition, saveChanges: true, cancellationToken))
            .Tap(_ => UpdateContextItems(context, instanceTransition.Id))
            .Map(_ => StepOutcome.Continue());
    }

    /// <summary>
    /// Gets the transition key from context items or uses the default transition key.
    /// </summary>
    private static string GetTransitionKey(TransitionExecutionContext context)
    {
        return context.Items.TryGetValue("NextTransitionKey", out var v) &&
               v is string next &&
               !string.IsNullOrEmpty(next)
            ? next
            : context.TransitionKey;
    }

    /// <summary>
    /// Creates the instance transition record and finds the transition definition.
    /// </summary>
    private (InstanceTransition InstanceTransition, Definitions.Transition? Transition) CreateInstanceTransition(
        TransitionExecutionContext context,
        string transitionKey)
    {
        var instanceTransition = InstanceTransition.Create(
            guidGenerator.Create(),
            context.InstanceId,
            transitionKey,
            context.Instance.GetCurrentState,
            new JsonData(context.Data),
            new JsonData(JsonSerializer.Serialize(context.Headers)));

        var transition = context.Workflow.FindTransition(transitionKey);

        return (instanceTransition, transition);
    }

    /// <summary>
    /// Maps transition data using the data mapper service.
    /// </summary>
    private Task<Result<object?>> MapTransitionDataAsync(
        TransitionExecutionContext context,
        Definitions.Transition? transition,
        CancellationToken cancellationToken)
    {
        return transitionDataMapper.MapTransitionDataAsync(
            context.Data,
            transition,
            context.Workflow,
            context.Instance,
            runtimeInfoProvider,
            context.Headers,
            cancellationToken);
    }

    /// <summary>
    /// Adds mapped data to instance if available.
    /// </summary>
    private void AddMappedDataToInstance(
        TransitionExecutionContext context,
        object? mappedData,
        Definitions.Transition? transition)
    {
        if (mappedData != null)
        {
            context.Instance.AddData(
                guidGenerator.Create(),
                new JsonData(mappedData),
                transition?.VersionStrategy);
        }
    }

    /// <summary>
    /// Updates context items with transition record ID.
    /// </summary>
    private static void UpdateContextItems(TransitionExecutionContext context, Guid transitionId)
    {
        context.Items["TransitionRecordId"] = transitionId;
        context.Items.Remove("NextTransitionKey");
    }
}
