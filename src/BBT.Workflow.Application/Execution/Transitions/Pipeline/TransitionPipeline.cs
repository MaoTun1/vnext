using BBT.Workflow.Domain;
using BBT.Workflow.Execution.Planner;
using BBT.Workflow.Telemetry;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BBT.Workflow.Execution.Pipeline;

/// <summary>
/// Orchestrates the execution of transition lifecycle steps in a deterministic order.
/// Each step in the pipeline performs a specific operation during the transition.
/// Uses Result pattern for exception-free error handling.
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
    /// <param name="planner"></param>
    public TransitionPipeline(IEnumerable<ITransitionStep> steps, ILogger<TransitionPipeline> logger, IPipelinePlanner planner)
    {
        _steps = steps.OrderBy(s => s.Order).ToList();
        _logger = logger;
        _planner = planner;
    }

    /// <summary>
    /// Executes all pipeline steps in order for the given transition context.
    /// Returns Result to indicate success or failure without throwing exceptions.
    /// </summary>
    /// <param name="context">The transition execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A Result indicating success or failure of the pipeline execution.</returns>
    public async Task<Result> RunAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // Note: ForTransition scope is already created at Strategy level
        _logger.TransitionStarted(
            TelemetryConstants.Prefixes.Execution,
            context.TransitionKey,
            context.InstanceId,
            context.WorkflowKey);

        // Create span for pipeline execution
        using var pipelineActivity = WorkflowActivitySource.Instance.StartActivity(
            TelemetryConstants.SpanNames.PipelineExecution,
            ActivityKind.Internal);

        var plan = _planner.Build(context, _steps);
        var i = 0;
        
        while (i < plan.Count)
        {
            if (context.SkipImmediateExecution) break;

            var step = plan[i];
            var stepName = step.GetType().Name;
            var stepOrder = step.Order;
            var sw = Stopwatch.StartNew();
            
            // Create span for each step
            using var stepActivity = WorkflowActivitySource.Instance.StartActivity(
                TelemetryConstants.SpanNames.PipelineStep,
                ActivityKind.Internal);
            
            stepActivity?.SetTag(TelemetryConstants.TagNames.StepName, stepName);
            stepActivity?.SetTag(TelemetryConstants.TagNames.StepOrder, stepOrder);
            stepActivity?.SetDisplayName($"[{stepOrder}] {stepName}");
            
            _logger.PipelineStepStarted(TelemetryConstants.Prefixes.Execution, stepOrder, stepName, context.InstanceId);
            
            // Execute step and handle Result
            var outcomeResult = await step.ExecuteAsync(context, cancellationToken);
            sw.Stop();
            
            if (!outcomeResult.IsSuccess)
            {
                stepActivity?.RecordExceptionWithStatus(new Exception(outcomeResult.Error.Message ?? outcomeResult.Error.Code));
                stepActivity?.AddTag("error.code", outcomeResult.Error.Code);
                
                _logger.PipelineStepFailed(
                    new Exception(outcomeResult.Error.Message ?? outcomeResult.Error.Code), 
                    TelemetryConstants.Prefixes.Execution, 
                    stepOrder, 
                    stepName, 
                    context.InstanceId);
                
                return Result.Fail(outcomeResult.Error);
            }
            
            var outcome = outcomeResult.Value!;
            _logger.PipelineStepCompleted(TelemetryConstants.Prefixes.Execution, stepOrder, stepName, context.InstanceId, sw.ElapsedMilliseconds);

            // Apply if step changed directives
            outcome.MutateDirectives?.Invoke(context.Directives);

            // 1) Stop?
            if (outcome.StopPipeline) break;

            // 2) SkipTo? (e.g. start over from CreateTransition after inline auto)
            if (outcome.SkipToOrder is { } skipTo)
            {
                // Create a plan from scratch based on a new beginning
                context.Directives.RequestResumeFrom(skipTo);
                plan = _planner.Build(context, _steps);
                i = 0;
                continue;
            }

            // 3) If the directive changes (e.g., epilogue becomes RUN→SKIP, subflow starts),
            // update the plan immediately.
            if (NeedsReplan(plan, context.Directives))
            {
                context.Directives.RequestResumeFrom(step.Order + 1);
                plan = _planner.Build(context, _steps);
                i = 0;
                continue;
            }

            i++; // go to next step
        }

        _logger.LogDebug("Completed transition pipeline for {WorkflowKey}.{TransitionKey} on instance {InstanceId}",
            context.WorkflowKey, context.TransitionKey, context.InstanceId);
        
        return Result.Ok();
    }
    
    private static bool NeedsReplan(IReadOnlyList<ITransitionStep> currentPlan, PipelineDirectives d)
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
