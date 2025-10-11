using BBT.Workflow.Instances;
using BBT.Workflow.SubFlow;

namespace BBT.Workflow.Execution.Pipeline.Steps;

public class ForwardToActiveSubflowStep(
    ISubflowForwardingService  subflowForwardingService) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Preflight;

    public async Task<StepOutcome> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Instance is { HasActiveSubFlow: false, Subflow: null })
        {
            return StepOutcome.Continue();
        }

        var (forwarded, status) = await subflowForwardingService.TryForwardTransitionAsync(
            context.Instance.Subflow!.SubFlowInstanceId,
            context.TransitionKey,
            new TransitionInput(
                context.Instance.Subflow!.SubFlowDomain,
                context.Instance.Subflow!.SubFlowName,
                context.Instance.Subflow!.SubFlowVersion,
                context.Data,
                true // sync = true
                )
            {
                Headers = context.Headers.ToDictionary(),
                RouteValues = context.RouteValues.ToDictionary(),
            },
            cancellationToken);
        
        if (!forwarded)
            return StepOutcome.Continue();
        
        context.ClientResponse = new ClientResponse
        {
            Id = context.InstanceId,
            Status = status ?? context.Instance.Status
        };
        
        return StepOutcome.Stop();
    }
}