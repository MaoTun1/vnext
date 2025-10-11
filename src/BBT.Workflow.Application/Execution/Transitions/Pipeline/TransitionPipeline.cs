using BBT.Workflow.Execution.Planner;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Pipeline;

/// <summary>
/// Orchestrates the execution of transition lifecycle steps in a deterministic order.
/// Each step in the pipeline performs a specific operation during the transition.
/// </summary>
public sealed class TransitionPipeline
{
    private readonly IReadOnlyList<ITransitionStep> _steps;
    private readonly IPipelinePlanner _planner;
    private readonly ILogger<TransitionPipeline> _logger;

    /// <summary>
    /// Initializes a new instance of the TransitionPipeline.
    /// </summary>
    /// <param name="steps">The collection of pipeline steps to execute.</param>
    /// <param name="logger">Logger for pipeline execution tracking.</param>
    public TransitionPipeline(IEnumerable<ITransitionStep> steps, ILogger<TransitionPipeline> logger, IPipelinePlanner planner)
    {
        _steps = steps.OrderBy(s => s.Order).ToList();
        _logger = logger;
        _planner = planner;
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

        var plan = _planner.Build(context, _steps);
        var i = 0;
        
        while (i < plan.Count)
        {
            if (context.SkipImmediateExecution) break;

            var step = plan[i];
            var outcome = await step.ExecuteAsync(context, cancellationToken);

            // Apply if step changed directives
            outcome.MutateDirectives?.Invoke(context.Directives);

            // 1) Stop?
            if (outcome.StopPipeline) break;

            // 2) SkipTo? (e.g. start over from CreateTransition after inline auto)
            if (outcome.SkipToOrder is int skipTo)
            {
                // Create a plan from scratch based on a new beginning
                context.Directives.RequestResumeFrom(skipTo);
                plan = _planner.Build(context, _steps);
                i = 0;
                continue;
            }

            // 3) If the directive changes (e.g., epilogue becomes RUN→SKIP, subflow starts),
            // update the plan immediately.
            if (NeedsReplan(step, plan, context.Directives))
            {
                context.Directives.RequestResumeFrom(step.Order + 1);
                plan = _planner.Build(context, _steps);
                i = 0;
                continue;
            }

            i++; // go to next step
        }

        // foreach (var step in _steps)
        // {
        //     // Check if we should skip immediate execution (for scheduled transitions)
        //     if (context.SkipImmediateExecution)
        //     {
        //         _logger.LogDebug("Skipping immediate execution for scheduled transition {TransitionKey}",
        //             context.TransitionKey);
        //         break;
        //     }
        //     
        //     if (startOrder != int.MinValue && step.Order < startOrder) continue;
        //
        //     try
        //     {
        //         _logger.LogTrace("Executing pipeline step {StepType} (order: {Order}) for transition {TransitionKey}",
        //             step.GetType().Name, step.Order, context.TransitionKey);
        //
        //         await step.ExecuteAsync(context, cancellationToken);
        //         context.Items.Remove("ResumeFrom");
        //         _logger.LogTrace("Completed pipeline step {StepType} for transition {TransitionKey}",
        //             step.GetType().Name, context.TransitionKey);
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Pipeline step {StepType} failed for transition {TransitionKey} on instance {InstanceId}",
        //             step.GetType().Name, context.TransitionKey, context.InstanceId);
        //         throw;
        //     }
        // }

        _logger.LogDebug("Completed transition pipeline for {WorkflowKey}.{TransitionKey} on instance {InstanceId}",
            context.WorkflowKey, context.TransitionKey, context.InstanceId);
    }
    
    private static bool NeedsReplan(ITransitionStep current, IReadOnlyList<ITransitionStep> currentPlan, PipelineDirectives d)
    {
        // A simple heuristic: replan if terminal is reached or epilogue mode is switched to SKIP.
        if (d.TerminalReached) return true;
        if (d.Epilogue == EpilogueMode.Skip &&
            currentPlan.Any(s => s.Order == LifecycleOrder.Schedule || s.Order == LifecycleOrder.Auto))
            return true;
        if (d.ResumeFromOrder is not null) return true;
        return false;
    }
}
