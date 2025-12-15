using System.Diagnostics;
using BBT.Aether.Aspects;
using BBT.Aether.Results;
using BBT.Workflow.Instances;
using BBT.Workflow.SubFlow;
using BBT.Workflow.Logging;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that forwards transitions to active subflow instances.
/// Uses Result pattern for exception-free error handling.
/// </summary>
public class ForwardToActiveSubflowStep(
    ISubflowForwardingService subflowForwardingService) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.ForwardToActiveSubflow;

    /// <inheritdoc />
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(ForwardToActiveSubflowStep)}");

        // Skip if no active subflow - early return for non-applicable case
        if (!HasActiveSubflow(context))
        {
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        // Railway chain: Forward -> Update context -> Create outcome
        return await Result.Ok(context)
            .Map(CreateTransitionInput)
            .BindAsync(input => ForwardToSubflowAsync(context, input, cancellationToken))
            .Map(result => CreateStepOutcome(context, result));
    }

    /// <summary>
    /// Checks if context has an active subflow.
    /// </summary>
    private static bool HasActiveSubflow(TransitionExecutionContext context)
        => context.Instance.HasActiveSubFlow || context.Instance.Subflow != null;

    /// <summary>
    /// Creates the transition input for subflow forwarding.
    /// </summary>
    private static TransitionInput CreateTransitionInput(TransitionExecutionContext context)
    {
        return new TransitionInput(
            context.Instance.Subflow!.SubFlowDomain,
            context.Instance.Subflow!.SubFlowName,
            context.Instance.Subflow!.SubFlowVersion,
            new TransitionDataInput(context.DataElement)
            {
                Key = context.InstanceKey,
                Tags = context.Tags
            },
            true // sync = true
        )
        {
            Headers = context.Headers.ToDictionary(),
            RouteValues = context.RouteValues.ToDictionary(),
        };
    }

    /// <summary>
    /// Forwards transition to subflow and returns forwarding result.
    /// </summary>
    private async Task<Result<SubflowForwardingResult>> ForwardToSubflowAsync(
        TransitionExecutionContext context,
        TransitionInput input,
        CancellationToken cancellationToken)
    {
        var (forwarded, status) = await subflowForwardingService.TryForwardTransitionAsync(
            context.Instance.Subflow!.SubFlowInstanceId,
            context.TransitionKey,
            input,
            cancellationToken);

        return Result<SubflowForwardingResult>.Ok(new SubflowForwardingResult(forwarded, status));
    }

    /// <summary>
    /// Creates step outcome based on forwarding result.
    /// </summary>
    private static StepOutcome CreateStepOutcome(
        TransitionExecutionContext context,
        SubflowForwardingResult result)
    {
        if (!result.Forwarded)
        {
            return StepOutcome.Continue();
        }

        context.ClientResponse = new ClientResponse
        {
            Id = context.InstanceId,
            Status = result.Status ?? context.Instance.Status
        };

        return StepOutcome.Stop();
    }

    /// <summary>
    /// Encapsulates subflow forwarding result.
    /// </summary>
    private sealed record SubflowForwardingResult(bool Forwarded, InstanceStatus? Status);
}
