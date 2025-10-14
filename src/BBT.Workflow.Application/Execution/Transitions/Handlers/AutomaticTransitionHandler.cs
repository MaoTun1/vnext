using BBT.Workflow.Definitions;
using BBT.Workflow.ExceptionHandling;
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
    protected override async Task PreValidateAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Validating automatic transition {TransitionKey} for instance {InstanceId}",
            context.TransitionKey, context.InstanceId);

        // For automatic transitions, we need to validate that the condition is met
        // This is a double-check since the condition was already evaluated in RunAutomaticTransitionsStep
        await ValidateConditionAsync(context, cancellationToken);

        // Additional validations for automatic transitions
        await ValidateSystemStateAsync(context);

        Logger.LogDebug("Automatic transition validation completed for {TransitionKey}", context.TransitionKey);
    }

    /// <summary>
    /// Validates that the automatic transition condition is met.
    /// </summary>
    private async Task ValidateConditionAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Transition?.Rule == null)
        {
            Logger.LogTrace("No condition defined for automatic transition {TransitionKey}, allowing execution",
                context.TransitionKey);
            return;
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
                Logger.LogDebug("Condition evaluation failed for automatic transition {TransitionKey} on instance {InstanceId}",
                    context.TransitionKey, context.InstanceId);
                throw new TransitionRuleFailedException(context.TransitionKey, "Transition rule evaluation failed");
            }

            Logger.LogTrace("Condition re-validation successful for automatic transition {TransitionKey}",
                context.TransitionKey);
        }
        catch (TransitionRuleFailedException)
        {
            throw;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            Logger.LogError(ex, "Failed to re-validate condition for automatic transition {TransitionKey}",
                context.TransitionKey);
            throw new InvalidOperationException(
                $"Failed to validate condition for automatic transition {context.TransitionKey}", ex);
        }
    }

    /// <summary>
    /// Validates the system state for automatic transition execution.
    /// </summary>
    private async Task ValidateSystemStateAsync(TransitionExecutionContext context)
    {
        // Basic validation: ensure we're not exceeding chain depth limits
        // (This is also checked in the dispatcher, but we double-check here)
        const int maxChainDepth = 50; // This should come from configuration
        if (context.ChainDepth > maxChainDepth)
        {
            throw new InvalidOperationException(
                $"Maximum chain depth ({maxChainDepth}) exceeded for automatic transition {context.TransitionKey}");
        }

        await Task.CompletedTask;
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