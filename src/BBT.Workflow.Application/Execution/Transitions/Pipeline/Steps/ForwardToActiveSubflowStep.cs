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
    [Log]
    [Trace]
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        Activity.Current?.SetDisplayName($"[{Order}] {nameof(ForwardToActiveSubflowStep)}");

        // Skip if no active subflow
        if (context.Instance is { HasActiveSubFlow: false, Subflow: null })
        {
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        // Railway Oriented Programming: Chain operations, each wrapped in Try
        return await ForwardTransitionToSubflow(context, cancellationToken)
            .ThenAsync(result => UpdateContextIfForwarded(context, result));
    }

    /// <summary>
    /// Forwards the transition to the active subflow instance.
    /// </summary>
    private async Task<Result<SubflowForwardingResult>> ForwardTransitionToSubflow(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var transitionInput = CreateTransitionInput(context);

        var forwardResult = await ResultExtensions.TryAsync(
            async ct =>
            {
                var (forwarded, status) = await subflowForwardingService.TryForwardTransitionAsync(
                    context.Instance.Subflow!.SubFlowInstanceId,
                    context.TransitionKey,
                    transitionInput,
                    ct);

                return new SubflowForwardingResult(forwarded, status);
            },
            cancellationToken);

        return forwardResult;
    }

    /// <summary>
    /// Creates the transition input for subflow forwarding.
    /// </summary>
    private TransitionInput CreateTransitionInput(TransitionExecutionContext context)
    {
        return new TransitionInput(
            context.Instance.Subflow!.SubFlowDomain,
            context.Instance.Subflow!.SubFlowName,
            context.Instance.Subflow!.SubFlowVersion,
            context.DataElement,
            true // sync = true
        )
        {
            Headers = context.Headers.ToDictionary(),
            RouteValues = context.RouteValues.ToDictionary(),
        };
    }

    /// <summary>
    /// Updates context if transition was forwarded and determines the step outcome.
    /// </summary>
    private Task<Result<StepOutcome>> UpdateContextIfForwarded(
        TransitionExecutionContext context,
        SubflowForwardingResult result)
    {
        if (!result.Forwarded)
        {
            return Task.FromResult(Result<StepOutcome>.Ok(StepOutcome.Continue()));
        }

        context.ClientResponse = new ClientResponse
        {
            Id = context.InstanceId,
            Status = result.Status ?? context.Instance.Status
        };

        return Task.FromResult(Result<StepOutcome>.Ok(StepOutcome.Stop()));
    }

    /// <summary>
    /// Encapsulates subflow forwarding result.
    /// </summary>
    private sealed record SubflowForwardingResult(bool Forwarded, InstanceStatus? Status);
}