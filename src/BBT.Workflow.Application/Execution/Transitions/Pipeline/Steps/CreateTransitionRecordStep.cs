using System.Diagnostics;
using BBT.Aether.Guids;
using BBT.Workflow.Execution.Transitions.Services;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Workflow.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that creates and persists the transition record.
/// This step tracks the transition attempt and provides audit trail.
/// Uses Result pattern for exception-free error handling.
/// </summary>
public sealed class CreateTransitionRecordStep(
    IInstanceTransitionRepository instanceTransitionRepository,
    IInstanceRepository instanceRepository,
    IGuidGenerator guidGenerator,
    ITransitionDataMapper transitionDataMapper,
    IRuntimeInfoProvider runtimeInfoProvider,
    ILogger<CreateTransitionRecordStep> logger) : ITransitionStep
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
            logger.LogDebug("Skipping transition record creation for SubFlow resume on instance {InstanceId}",
                context.InstanceId);
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        // Railway Oriented Programming: Chain operations, each wrapped in Try
        return await GetTransitionKey(context)
            .ThenAsync(key => CreateInstanceTransition(context, key))
            .ThenAsync(info => MapTransitionData(context, info, cancellationToken))
            .ThenAsync(info => AddMappedDataToInstance(context, info))
            .ThenAsync(info => UpdateInstanceInRepository(context, info, cancellationToken))
            .ThenAsync(info => PersistTransitionRecord(info, cancellationToken))
            .OnSuccess(info => UpdateContextItems(context, info))
            .ThenAsync(_ => Task.FromResult(Result<StepOutcome>.Ok(StepOutcome.Continue())));
    }

    /// <summary>
    /// Gets the transition key from context items or uses the default transition key.
    /// </summary>
    private Task<Result<string>> GetTransitionKey(TransitionExecutionContext context)
    {
        return Task.FromResult(
            ResultExtensions.Try(() =>
            {
                var transitionKey = context.Items.TryGetValue("NextTransitionKey", out var v) &&
                                    v is string next &&
                                    !string.IsNullOrEmpty(next)
                    ? next
                    : context.TransitionKey;

                return transitionKey;
            })
        );
    }

    /// <summary>
    /// Creates the instance transition record and finds the transition definition.
    /// </summary>
    private Task<Result<TransitionRecordInfo>> CreateInstanceTransition(
        TransitionExecutionContext context,
        string transitionKey)
    {
        return Task.FromResult(
            ResultExtensions.Try(() =>
            {
                var instanceTransition = InstanceTransition.Create(
                    guidGenerator.Create(),
                    context.InstanceId,
                    transitionKey,
                    context.Instance.GetCurrentState,
                    new JsonData(context.Data),
                    new JsonData(JsonSerializer.Serialize(context.Headers))
                );

                var transition = context.Workflow.FindTransition(transitionKey);

                return new TransitionRecordInfo(
                    instanceTransition,
                    transition,
                    null);
            })
        );
    }

    /// <summary>
    /// Maps transition data using optional mapping script.
    /// </summary>
    private async Task<Result<TransitionRecordInfo>> MapTransitionData(
        TransitionExecutionContext context,
        TransitionRecordInfo info,
        CancellationToken cancellationToken)
    {
        var mappedDataResult = await transitionDataMapper.MapTransitionDataAsync(
            context.Data,
            info.Transition,
            context.Workflow,
            context.Instance,
            runtimeInfoProvider,
            context.Headers,
            cancellationToken);

        if (!mappedDataResult.IsSuccess)
        {
            return Result<TransitionRecordInfo>.Fail(mappedDataResult.Error);
        }

        return Result<TransitionRecordInfo>.Ok(info with { MappedData = mappedDataResult.Value });
    }

    /// <summary>
    /// Adds mapped data to instance if mapping result is not null.
    /// </summary>
    private Task<Result<TransitionRecordInfo>> AddMappedDataToInstance(
        TransitionExecutionContext context,
        TransitionRecordInfo info)
    {
        return Task.FromResult(
            ResultExtensions.Try(() =>
            {
                if (info.MappedData != null)
                {
                    context.Instance.AddData(
                        guidGenerator.Create(),
                        new JsonData(info.MappedData),
                        info.Transition?.VersionStrategy
                    );
                }

                return info;
            })
        );
    }

    /// <summary>
    /// Updates the instance in repository.
    /// </summary>
    private async Task<Result<TransitionRecordInfo>> UpdateInstanceInRepository(
        TransitionExecutionContext context,
        TransitionRecordInfo info,
        CancellationToken cancellationToken)
    {
        var updateResult = await ResultExtensions.TryAsync(
            async ct => await instanceRepository.UpdateAsync(context.Instance, true, ct),
            cancellationToken);

        return updateResult.IsSuccess
            ? Result<TransitionRecordInfo>.Ok(info)
            : Result<TransitionRecordInfo>.Fail(updateResult.Error);
    }

    /// <summary>
    /// Persists the transition record to repository.
    /// </summary>
    private async Task<Result<TransitionRecordInfo>> PersistTransitionRecord(
        TransitionRecordInfo info,
        CancellationToken cancellationToken)
    {
        var insertResult = await ResultExtensions.TryAsync(
            async ct => await instanceTransitionRepository.InsertAsync(info.InstanceTransition, saveChanges: true, ct),
            cancellationToken);

        return insertResult.IsSuccess
            ? Result<TransitionRecordInfo>.Ok(info)
            : Result<TransitionRecordInfo>.Fail(insertResult.Error);
    }

    /// <summary>
    /// Updates context items with transition record ID and removes next transition key.
    /// </summary>
    private void UpdateContextItems(TransitionExecutionContext context, TransitionRecordInfo info)
    {
        context.Items["TransitionRecordId"] = info.InstanceTransition.Id;
        context.Items.Remove("NextTransitionKey");
    }
    
    /// <summary>
    /// Encapsulates transition record creation information.
    /// </summary>
    private sealed record TransitionRecordInfo(
        InstanceTransition InstanceTransition,
        Definitions.Transition? Transition,
        object? MappedData);
}