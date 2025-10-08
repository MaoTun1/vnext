using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline;

/// <summary>
/// Orchestrates the execution of transition lifecycle steps in a deterministic order.
/// Each step in the pipeline performs a specific operation during the transition.
/// </summary>
public sealed class TransitionPipeline
{
    private readonly IReadOnlyList<ITransitionStep> _steps;
    private readonly ILogger<TransitionPipeline> _logger;

    /// <summary>
    /// Initializes a new instance of the TransitionPipeline.
    /// </summary>
    /// <param name="steps">The collection of pipeline steps to execute.</param>
    /// <param name="logger">Logger for pipeline execution tracking.</param>
    public TransitionPipeline(IEnumerable<ITransitionStep> steps, ILogger<TransitionPipeline> logger)
    {
        _steps = steps.OrderBy(s => s.Order).ToList();
        _logger = logger;
    }

    /// <summary>
    /// Executes all pipeline steps in order for the given transition context.
    /// </summary>
    /// <param name="context">The transition execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting transition pipeline for {WorkflowKey}.{TransitionKey} on instance {InstanceId}",
            context.WorkflowKey, context.TransitionKey, context.InstanceId);

        foreach (var step in _steps)
        {
            // Check if we should skip immediate execution (for scheduled transitions)
            if (context.SkipImmediateExecution)
            {
                _logger.LogDebug("Skipping immediate execution for scheduled transition {TransitionKey}",
                    context.TransitionKey);
                break;
            }

            try
            {
                _logger.LogTrace("Executing pipeline step {StepType} (order: {Order}) for transition {TransitionKey}",
                    step.GetType().Name, step.Order, context.TransitionKey);

                await step.ExecuteAsync(context, cancellationToken);

                _logger.LogTrace("Completed pipeline step {StepType} for transition {TransitionKey}",
                    step.GetType().Name, context.TransitionKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipeline step {StepType} failed for transition {TransitionKey} on instance {InstanceId}",
                    step.GetType().Name, context.TransitionKey, context.InstanceId);
                throw;
            }
        }

        _logger.LogDebug("Completed transition pipeline for {WorkflowKey}.{TransitionKey} on instance {InstanceId}",
            context.WorkflowKey, context.TransitionKey, context.InstanceId);
    }
}
