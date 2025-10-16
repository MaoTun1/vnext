using System.Diagnostics;
using BBT.Aether.Guids;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using BBT.Workflow.SubFlow;
using BBT.Workflow.Telemetry;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that handles SubFlow operations.
/// Manages sub-process initiation when the target state type is SubFlow.
/// </summary>
public sealed class HandleSubFlowStep(
    IInstanceRepository instanceRepository,
    ISubflowStarter subflowStarter,
    IGuidGenerator guidGenerator,
    IScriptContextFactory scriptContextFactory,
    ILogger<HandleSubFlowStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.SubFlow;

    /// <inheritdoc />
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // Early return if this step is not applicable
        if (!IsApplicable(context))
            return Result<StepOutcome>.Ok(StepOutcome.Continue());

        // If applicable, proceed with the railway
        return await ValidateConfigurationAsync(context)
            .ThenAsync(_ => LogSubFlowHandlingStartAsync(context))
            .ThenAsync(_ => HandleSubFlowAsync(context, cancellationToken))
            .OnSuccessAsync(_ => LogSubFlowHandlingCompletedAsync(context))
            .ThenAsync(_ => CreateStepOutcomeAsync(context))
            .OnFailureAsync(error => LogSubFlowHandlingFailedAsync(context, error));
    }

    /// <summary>
    /// Checks if this step is applicable for the given context.
    /// Returns false if target is null or not a SubFlow state type.
    /// </summary>
    private bool IsApplicable(TransitionExecutionContext context)
    {
        if (context.Target == null)
        {
            logger.LogWarning("Target state is null for instance {InstanceId}", context.InstanceId);
            return false;
        }

        // Only handle SubFlow state types
        if (context.Target.StateType != StateType.SubFlow)
        {
            logger.LogTrace("State {StateName} is not a SubFlow type, skipping SubFlow handling",
                context.Target.Key);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates the SubFlow configuration.
    /// </summary>
    private Task<Result<TransitionExecutionContext>> ValidateConfigurationAsync(TransitionExecutionContext context)
    {
        if (context.Target!.SubFlow == null)
        {
            logger.LogError("No SubFlow defined for state {StateName} on instance {InstanceId}", 
                context.Target.Key, context.InstanceId);
            return Task.FromResult(Result<TransitionExecutionContext>.Fail(
                Error.Validation(
                    WorkflowErrorCodes.ConfigInvalid,
                    $"SubFlow configuration not found for state {context.Target.Key} on instance {context.InstanceId}")));
        }

        return Task.FromResult(Result<TransitionExecutionContext>.Ok(context));
    }

    /// <summary>
    /// Logs the start of SubFlow handling.
    /// </summary>
    private Task<Result<TransitionExecutionContext>> LogSubFlowHandlingStartAsync(TransitionExecutionContext context)
    {
        logger.LogDebug("Handling SubFlow for state {StateName} on instance {InstanceId}",
            context.Target!.Key, context.InstanceId);
        
        return Task.FromResult(Result<TransitionExecutionContext>.Ok(context));
    }

    /// <summary>
    /// Logs the completion of SubFlow handling.
    /// </summary>
    private Task LogSubFlowHandlingCompletedAsync(TransitionExecutionContext context)
    {
        logger.LogDebug("Completed SubFlow handling for state {StateName}", context.Target!.Key);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs SubFlow handling failures.
    /// </summary>
    private Task LogSubFlowHandlingFailedAsync(TransitionExecutionContext context, Error error)
    {
        logger.LogError("Failed to handle SubFlow for state {StateName} on instance {InstanceId}: {ErrorCode} - {ErrorMessage}",
            context.Target?.Key, context.InstanceId, error.Code, error.Message);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates the appropriate StepOutcome based on SubFlow type.
    /// </summary>
    private Task<Result<StepOutcome>> CreateStepOutcomeAsync(TransitionExecutionContext context)
    {
        if (context.Target!.SubFlow!.Type.Equals(SubFlowType.SubFlow))
        {
            // Skip the epilogue, go to the finale, then stop the pipeline
            var outcome = new StepOutcome
            {
                MutateDirectives = d =>
                {
                    d.RequestEpilogue(EpilogueMode.Skip);
                    d.MarkTerminal();
                },
                SkipToOrder = LifecycleOrder.Finalize
            };
            return Task.FromResult(Result<StepOutcome>.Ok(outcome));
        }
        
        return Task.FromResult(Result<StepOutcome>.Ok(StepOutcome.Continue()));
    }

    /// <summary>
    /// Handles SubFlow operations using Railway Oriented Programming.
    /// </summary>
    private async Task<Result<TransitionExecutionContext>> HandleSubFlowAsync(
        TransitionExecutionContext context, 
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(
            async ct => await ExecuteSubFlowOperationsAsync(context, ct),
            cancellationToken,
            ex => Error.Failure(
                WorkflowErrorCodes.ExecutionStepFailed,
                $"Failed to handle SubFlow: {ex.Message}",
                ex.GetType().Name));
    }

    /// <summary>
    /// Executes the SubFlow operations: creates correlation, updates instance, and starts SubFlow.
    /// </summary>
    private async Task<TransitionExecutionContext> ExecuteSubFlowOperationsAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Handling SubFlow {SubFlowType} for state {StateName} on instance {InstanceId}",
            context.Target!.SubFlow!.Type, context.Target.Key, context.InstanceId);

        // Handle the SubFlow
        // Create correlation to track SubFlow/SubProcess instance
        // SubFlow (Type "S"): Blocks parent workflow until completion
        // SubProcess (Type "P"): Runs in parallel without blocking parent
        
        //TODO: Bu creation'ı Instance içine al
        var correlation = InstanceCorrelation.Create(
            guidGenerator.Create(),
            context.InstanceId,
            context.Target.Key,
            guidGenerator.Create(),
            context.Target!.SubFlow!.Type.Code,
            context.Target.SubFlow.Process.Domain,
            context.Target.SubFlow.Process.Key,
            context.Target.SubFlow.Process.Version);

        context.Instance.AddCorrelation(correlation);
        
        var updateResult = await ResultExtensions.TryAsync(
            async ct => await instanceRepository.UpdateAsync(context.Instance, true, ct),
            cancellationToken,
            ex => Error.Dependency("db.update", $"Failed to add correlation: {ex.Message}"));
        
        if (!updateResult.IsSuccess)
            throw new InvalidOperationException($"Failed to add correlation: {updateResult.Error.Message}");

        // Create script context for SubFlow handling
        var scriptContext = context.GetOrBuildScriptContext(() =>
            CreateScriptContext(context));

        await subflowStarter.StartAsync(
            context.Workflow,
            context.Instance,
            context.Target,
            context.Transition!, // Transition is required for SubFlow handling
            correlation,
            scriptContext,
            cancellationToken
        );

        // Record SubFlow initiation as an event
        Activity.Current?.AddEvent(new ActivityEvent("subflow.initiated",
            tags: new ActivityTagsCollection
            {
                { TelemetryConstants.TagNames.SubFlowKey, context.Target.SubFlow.Process.Key },
                { TelemetryConstants.TagNames.Domain, context.Target.SubFlow.Process.Domain },
                { "workflow.subflow.type", context.Target.SubFlow.Type.Code },
                { "workflow.subflow.version", context.Target.SubFlow.Process.Version?.ToString() ?? "latest" },
                { "workflow.correlation.id", correlation.Id.ToString() },
                { "workflow.subflow.instance.id", correlation.SubFlowInstanceId.ToString() }
            }));

        logger.LogDebug(
            "SubFlow {SubFlowKey} initiated with correlation {CorrelationId} and instance {SubFlowInstanceId}",
            context.Target.SubFlow.Process.Key, correlation.Id, correlation.SubFlowInstanceId);

        return context;
    }

    /// <summary>
    /// Creates a script context for SubFlow operations.
    /// </summary>
    private ScriptContext CreateScriptContext(TransitionExecutionContext context)
    {
        // This would use the script context factory to create a proper context
        // For now, we'll get it from the context cache
        return scriptContextFactory.NewBuilder()
            .WithWorkflow(context.Workflow)
            .WithInstance(context.Instance)
            .WithTransition(context.Transition!) // Transition is required for SubFlow handling
            .WithBody(context.Data)
            .WithHeaders(context.Headers.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value))
            .BuildAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}