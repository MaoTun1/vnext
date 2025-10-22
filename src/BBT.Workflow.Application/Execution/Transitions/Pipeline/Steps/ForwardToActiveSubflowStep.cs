using System.Text.Json;
using BBT.Workflow.Domain;
using BBT.Workflow.Instances;
using BBT.Workflow.SubFlow;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that forwards transitions to active subflow instances.
/// Uses Result pattern for exception-free error handling.
/// </summary>
public class ForwardToActiveSubflowStep(
    ISubflowForwardingService  subflowForwardingService) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.Preflight;

    /// <inheritdoc />
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Instance is { HasActiveSubFlow: false, Subflow: null })
        {
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        return await ResultExtensions.TryAsync<StepOutcome>(async ct =>
        {
            var (forwarded, status) = await subflowForwardingService.TryForwardTransitionAsync(
                context.Instance.Subflow!.SubFlowInstanceId,
                context.TransitionKey,
                new TransitionInput(
                    context.Instance.Subflow!.SubFlowDomain,
                    context.Instance.Subflow!.SubFlowName,
                    context.Instance.Subflow!.SubFlowVersion,
                    context.DataElement,
                    true // sync = true
                    )
                {
                    Headers = context.Headers.ToDictionary(),
                    RouteValues = context.RouteValues.ToDictionary(),
                },
                ct);
            
            if (!forwarded)
                return StepOutcome.Continue();
            
            context.ClientResponse = new ClientResponse
            {
                Id = context.InstanceId,
                Status = status ?? context.Instance.Status
            };
            
            return StepOutcome.Stop();
        },
        cancellationToken,
        ex => Error.Failure(
            WorkflowErrorCodes.ExecutionStepFailed,
            $"Failed to forward transition to subflow: {ex.Message}",
            ex.GetType().Name));
    }
}