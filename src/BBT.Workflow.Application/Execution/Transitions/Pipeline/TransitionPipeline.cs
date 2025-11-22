using Microsoft.Extensions.Logging;
using BBT.Aether.Aspects;
using BBT.Aether.Results;

namespace BBT.Workflow.Execution.Pipeline;

/// <summary>
/// Orchestrates the execution of transition lifecycle steps in a deterministic order.
/// Each step in the pipeline performs a specific operation during the transition.
/// Uses Result pattern for exception-free error handling.
/// </summary>
public class TransitionPipeline
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
    /// Returns Result to indicate success or failure without throwing exceptions.
    /// Uses Railway Programming pattern for declarative error handling and step composition.
    /// </summary>
    /// <param name="context">The transition execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A Result indicating success or failure of the pipeline execution.</returns>
    [Log]
    [Trace]
    public async Task<Result> RunAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        var plan = BuildExecutionPlan(context);
        var state = new PipelineState(plan, 0);

        while (state.HasMoreSteps())
        {
            if (context.SkipImmediateExecution)
                return Result.Ok();

            var step = state.CurrentStep;

            // Railway Programming: Execute step → Log on failure → Handle outcome
            var outcomeResult = await ExecuteStepAsync(step, context, cancellationToken);

            if (!outcomeResult.IsSuccess)
            {
                return Result.Fail(outcomeResult.Error);
            }

            var flowControlResult = await HandleStepOutcomeAsync(outcomeResult.Value!, step, context, state);

            if (!flowControlResult.IsSuccess)
                return flowControlResult.ToResult();

            // Check if we need to replan and restart
            var flowControl = flowControlResult.Value!;
            if (flowControl.ShouldReplan)
            {
                state = new PipelineState(BuildExecutionPlan(context), 0);
                continue;
            }

            if (flowControl.ShouldStop)
                break;

            state = state.MoveNext();
        }

        return Result.Ok();
    }

    /// <summary>
    /// Builds an execution plan by filtering and ordering steps based on context directives.
    /// Handles resume points, epilogue modes, and terminal states.
    /// </summary>
    /// <param name="context">The transition execution context containing directives.</param>
    /// <returns>A filtered and ordered list of steps to execute.</returns>
    private IReadOnlyList<ITransitionStep> BuildExecutionPlan(TransitionExecutionContext context)
    {
        var ordered = _steps.ToList(); // Already ordered in constructor

        // 1) ResumeFrom start
        var startOrder = context.Directives.ConsumeResumeFrom(); // one-time
        if (startOrder.HasValue)
            ordered = ordered.Where(s => s.Order >= startOrder.Value).ToList();

        // 2) Subflow terminal short circuit (until Finalize)
        if (context.Directives.TerminalReached)
        {
            var maxOrder = LifecycleOrder.Finalize;
            ordered = ordered.Where(s => s.Order <= maxOrder).ToList();
        }

        // 3) Epilogue policy
        if (context.Directives.Epilogue == EpilogueMode.Skip)
        {
            ordered = ordered
                .Where(s => s.Order != LifecycleOrder.Schedule &&
                            s.Order != LifecycleOrder.Auto)
                .ToList();
        }

        return ordered;
    }

    /// <summary>
    /// Executes a single step.
    /// Returns Result containing step outcome, following Railway Programming pattern.
    /// </summary>
    private async Task<Result<StepOutcome>> ExecuteStepAsync(
        ITransitionStep step,
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Execute step and track result in telemetry
        return await step.ExecuteAsync(context, cancellationToken);
    }

    /// <summary>
    /// Handles step outcome and determines flow control.
    /// Applies directive mutations and determines if pipeline should stop, skip, or replan.
    /// Returns flow control decision wrapped in Result.
    /// </summary>
    private Task<Result<FlowControl>> HandleStepOutcomeAsync(
        StepOutcome outcome,
        ITransitionStep step,
        TransitionExecutionContext context,
        PipelineState state)
    {
        // Apply directive mutations from outcome
        outcome.MutateDirectives?.Invoke(context.Directives);

        // 1) Stop pipeline?
        if (outcome.StopPipeline)
            return Task.FromResult(Result<FlowControl>.Ok(FlowControl.Stop()));

        // 2) Skip to specific order? (e.g., restart from CreateTransition after inline auto)
        if (outcome.SkipToOrder is { } skipTo)
        {
            context.Directives.RequestResumeFrom(skipTo);
            return Task.FromResult(Result<FlowControl>.Ok(FlowControl.Replan()));
        }

        // 3) Directives changed requiring replan?
        if (NeedsReplan(state.Plan, context.Directives))
        {
            context.Directives.RequestResumeFrom(step.Order + 1);
            return Task.FromResult(Result<FlowControl>.Ok(FlowControl.Replan()));
        }

        // Continue to next step
        return Task.FromResult(Result<FlowControl>.Ok(FlowControl.Continue()));
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

    /// <summary>
    /// Represents the current execution state of the pipeline.
    /// Encapsulates plan and current position for Railway Programming pattern.
    /// </summary>
    private readonly record struct PipelineState(IReadOnlyList<ITransitionStep> Plan, int Index)
    {
        public ITransitionStep CurrentStep => Plan[Index];
        public bool HasMoreSteps() => Index < Plan.Count;
        public PipelineState MoveNext() => this with { Index = Index + 1 };
    }

    /// <summary>
    /// Represents flow control decision after step execution.
    /// Used in Railway Programming to determine next action in pipeline.
    /// </summary>
    private readonly record struct FlowControl(bool ShouldStop, bool ShouldReplan)
    {
        public static FlowControl Stop() => new(ShouldStop: true, ShouldReplan: false);
        public static FlowControl Replan() => new(ShouldStop: false, ShouldReplan: true);
        public static FlowControl Continue() => new(ShouldStop: false, ShouldReplan: false);
    }
}