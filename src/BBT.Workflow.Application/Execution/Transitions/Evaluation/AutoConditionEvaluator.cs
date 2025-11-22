using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using BBT.Workflow.Logging;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution;

/// <summary>
/// Evaluates automatic transition conditions using the scripting engine.
/// Handles condition evaluation errors gracefully using the Result pattern.
/// </summary>
public sealed class AutoConditionEvaluator(
    ITaskConditionService taskConditionService,
    IScriptContextFactory scriptContextFactory,
    ILogger<AutoConditionEvaluator> logger) : IAutoConditionEvaluator
{
    /// <inheritdoc />
    public async Task<Result<AutoConditionEvaluation>> EvaluateAsync(
        Transition transition,
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // Validate that the transition has a rule defined
        if (transition.Rule is null)
        {
            logger.AutoTransitionNoRule(
                TelemetryConstants.Prefixes.Execution,
                transition.Key);

            return Result<AutoConditionEvaluation>.Fail(
                Error.Validation(
                    WorkflowErrorCodes.ConfigInvalid,
                    $"Automatic transition '{transition.Key}' has no rule defined."));
        }

        try
        {
            // Build or retrieve cached ScriptContext
            var scriptContext = await context.GetOrBuildScriptContextAsync(
                ct => CreateScriptContextAsync(context, ct),
                cancellationToken);

            // Execute the condition script
            var conditionResult = await taskConditionService.ExecuteConditionAsync(
                transition.Rule,
                scriptContext,
                cancellationToken);
            

            var status = conditionResult
                ? AutoConditionStatus.Satisfied
                : AutoConditionStatus.NotSatisfied;

            var evaluation = new AutoConditionEvaluation(transition.Key, status);
            return Result<AutoConditionEvaluation>.Ok(evaluation);
        }
        catch (Exception ex)
        {
            logger.TransitionRuleFailed(
                TelemetryConstants.Prefixes.Execution,
                transition.Key,
                context.InstanceId,
                ex.Message);

            return Result<AutoConditionEvaluation>.Fail(
                Error.Failure(
                    WorkflowErrorCodes.TransitionRuleFailed,
                    $"Automatic transition rule evaluation failed for '{transition.Key}': {ex.Message}",
                    ex.ToString()));
        }
    }

    /// <summary>
    /// Creates a script context for condition evaluation.
    /// </summary>
    private async Task<ScriptContext> CreateScriptContextAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var builder = scriptContextFactory.NewBuilder()
            .WithWorkflow(context.Workflow)
            .WithInstance(context.Instance)
            .WithBody(context.Data)
            .WithHeaders(context.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        
        if (context.Transition != null)
            builder.WithTransition(context.Transition);
        
        return await builder.BuildAsync(cancellationToken);
    }
}

