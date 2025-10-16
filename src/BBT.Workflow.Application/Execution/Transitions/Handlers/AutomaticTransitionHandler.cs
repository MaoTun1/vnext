using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Execution.Validation;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Handlers;

/// <summary>
/// Handler for automatic transitions triggered by the system based on conditions.
/// Validates conditions before allowing transition execution.
/// </summary>
public sealed class AutomaticTransitionHandler(
    ITaskConditionService taskConditionService,
    IScriptContextFactory scriptContextFactory,
    ITransitionValidationService validationService,
    ILogger<AutomaticTransitionHandler> logger) : TransitionHandlerBase(logger, validationService)
{
    /// <inheritdoc />
    public override bool CanHandle(TriggerType triggerType) => triggerType == TriggerType.Automatic;

    /// <inheritdoc />
    protected override async Task<Result> PreValidateInternalAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Validating automatic transition {TransitionKey} for instance {InstanceId}",
            context.TransitionKey, context.InstanceId);

        // Additional validations for automatic transitions
        var systemStateResult = await ValidateSystemStateAsync(context);
        if (!systemStateResult.IsSuccess)
            return systemStateResult;
        
        // For automatic transitions, we need to validate that the condition is met
        // This is a double-check since the condition was already evaluated in RunAutomaticTransitionsStep
        var conditionResult = await ValidateConditionAsync(context, cancellationToken);
        if (!conditionResult.IsSuccess)
            return conditionResult;
        
        Logger.LogDebug("Automatic transition validation completed for {TransitionKey}", context.TransitionKey);
        return Result.Ok();
    }

    /// <summary>
    /// Validates that the automatic transition condition is met.
    /// Returns Result instead of throwing exception to support multi-auto-transition scenarios.
    /// </summary>
    private async Task<Result> ValidateConditionAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Transition?.Rule == null)
        {
            Logger.LogTrace("No condition defined for automatic transition {TransitionKey}, allowing execution",
                context.TransitionKey);
            return Result.Ok();
        }

        Logger.LogTrace("Re-validating condition for automatic transition {TransitionKey}", context.TransitionKey);

        try
        {
            // Get or build script context
            var scriptContext = context.GetOrBuildScriptContext(() =>
                CreateScriptContext(context));

            // Evaluate the condition
            var conditionMet = await taskConditionService.ExecuteConditionAsync(
                context.Transition.Rule,
                scriptContext,
                cancellationToken);

            if (!conditionMet)
            {
                Logger.LogDebug("Condition not met for automatic transition {TransitionKey} on instance {InstanceId} - this is normal in multi-auto-transition scenarios",
                    context.TransitionKey, context.InstanceId);
                
                // Return Result.Fail with special error code - NOT an exception
                // This allows upstream code to handle it gracefully in multi-auto-transition scenarios
                return Result.Fail(WorkflowErrors.AutoTransitionConditionNotMet(context.TransitionKey));
            }

            Logger.LogTrace("Condition re-validation successful for automatic transition {TransitionKey}",
                context.TransitionKey);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to re-validate condition for automatic transition {TransitionKey}",
                context.TransitionKey);
            return Result.Fail(
                Error.Failure(
                    WorkflowErrorCodes.ExecutionStepFailed,
                    $"Failed to validate condition for automatic transition {context.TransitionKey}: {ex.Message}",
                    ex.GetType().Name));
        }
    }

    /// <summary>
    /// Validates the system state for automatic transition execution.
    /// </summary>
    private Task<Result> ValidateSystemStateAsync(TransitionExecutionContext context)
    {
        // Basic validation: ensure we're not exceeding chain depth limits
        // (This is also checked in the dispatcher, but we double-check here)
        const int maxChainDepth = 50; // This should come from configuration
        if (context.ChainDepth > maxChainDepth)
        {
            return Task.FromResult(Result.Fail(
                Error.Validation(
                    WorkflowErrorCodes.ExecutionStepFailed,
                    $"Maximum chain depth ({maxChainDepth}) exceeded for automatic transition {context.TransitionKey}")));
        }

        return Task.FromResult(Result.Ok());
    }

    /// <inheritdoc />
    protected override async Task PostProcessAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Post-processing automatic transition {TransitionKey} for instance {InstanceId}",
            context.TransitionKey, context.InstanceId);

        await UpdateExecutionMetricsAsync(context, cancellationToken);
        await LogExecutionChainAsync(context);

        Logger.LogDebug("Automatic transition post-processing completed for {TransitionKey}", context.TransitionKey);
    }

    /// <summary>
    /// Updates execution metrics for automatic transitions.
    /// </summary>
    private async Task UpdateExecutionMetricsAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Logs execution chain information for debugging and monitoring.
    /// </summary>
    private async Task LogExecutionChainAsync(TransitionExecutionContext context)
    {
        Logger.LogInformation(
            "Automatic transition executed: {TransitionKey} on instance {InstanceId}, " +
            "chain {ExecutionChainId}, depth {ChainDepth}",
            context.TransitionKey, context.InstanceId, context.ExecutionChainId, context.ChainDepth);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a script context for condition evaluation.
    /// </summary>
    private ScriptContext CreateScriptContext(TransitionExecutionContext context)
    {
        return scriptContextFactory.NewBuilder()
            .WithWorkflow(context.Workflow)
            .WithInstance(context.Instance)
            .WithTransition(context.Transition!)
            .WithBody(context.Data)
            .WithHeaders(context.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
            .BuildAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}