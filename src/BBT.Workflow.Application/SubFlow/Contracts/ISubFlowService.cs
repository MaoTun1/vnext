using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.SubFlow;

/// <summary>
/// Interface for service managing SubFlow and SubProcess workflows.
/// This interface defines the contract for handling execution and correlation management
/// between parent workflow and SubFlow definitions. Both SubFlow and SubProcess now create separate instances.
/// </summary>
public interface ISubFlowService
{
    /// <summary>
    /// Handles the initiation of SubFlow execution.
    /// Both SubFlow and SubProcess now create separate instances via remote calls.
    /// </summary>
    /// <param name="parentInstance">The main workflow instance that initiates the sub-flow.</param>
    /// <param name="targetState">The target state containing SubFlow configuration.</param>
    /// <param name="context">The script context containing execution data and headers.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous sub-flow initiation operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the SubFlow configuration is invalid.</exception>
    /// <remarks>
    /// <para>
    /// SubFlow (Type: "S"): Creates a separate instance and blocks the parent workflow until completion. 
    /// The parent workflow cannot continue until the SubFlow is completed.
    /// </para>
    /// <para>
    /// SubProcess (Type: "P"): Creates a separate instance and runs in parallel without blocking the parent workflow. 
    /// The parent workflow can continue immediately after starting the SubProcess.
    /// </para>
    /// </remarks>
    Task HandleSubFlowAsync(
        Instance parentInstance,
        State targetState,
        ScriptContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the SubFlow workflow definition and its available transitions for the current state.
    /// This method checks for active SubFlow correlations.
    /// For SubFlow (Type "S"): Returns SubFlow running on main instance.
    /// For SubProcess (Type "P"): Returns blocking SubFlow correlations (unchanged behavior).
    /// </summary>
    /// <param name="instanceId">The workflow instance ID.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The SubFlow workflow definition and state information if active; otherwise, null.</returns>
    Task<(Definitions.Workflow SubFlowWorkflow, InstanceCorrelation Correlation)?> GetActiveSubFlowContextAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the SubFlow workflow definition and current state for SubFlow running on main instance.
    /// This method returns the SubFlow workflow and correlation when a SubFlow is active.
    /// </summary>
    /// <param name="instanceId">The workflow instance ID.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The SubFlow workflow definition and correlation if active; otherwise, null.</returns>
    Task<(Definitions.Workflow SubFlowWorkflow, InstanceCorrelation Correlation)?> GetActiveSubFlowOnMainInstanceAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a workflow instance has pending sub-flows that would block transitions.
    /// This method is used to determine if a transition can be executed or should be blocked.
    /// Only SubFlow type "S" instances block the parent workflow, SubProcess type "P" instances do not.
    /// </summary>
    /// <param name="instanceId">The unique identifier of the workflow instance to check.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous blocking check operation.
    /// The result is true if the instance has blocking sub-flows (Type "S"); otherwise, false.
    /// </returns>
    Task<bool> HasBlockingSubFlowsAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles the completion of a SubFlow by checking if the SubFlow instance has completed
    /// and updating the parent workflow accordingly.
    /// This method is now used primarily for managing SubFlow completion signals.
    /// </summary>
    /// <param name="instance">The workflow instance.</param>
    /// <param name="currentSubFlowState">The current state in the SubFlow that is finishing.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous completion handling operation.</returns>
    Task HandleSubFlowCompletionAsync(
        Instance instance,
        State currentSubFlowState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a SubFlow instance has completed by querying its status.
    /// This is a placeholder method that will be implemented with a dedicated endpoint.
    /// </summary>
    /// <param name="subFlowInstanceId">The SubFlow instance ID to check for completion.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>True if the SubFlow has completed; otherwise, false.</returns>
    /// <remarks>
    /// TODO: Implement this method to call a dedicated endpoint that checks SubFlow completion status.
    /// This endpoint will be responsible for determining if a SubFlow instance has reached a completion state.
    /// </remarks>
    Task<bool> IsSubFlowCompletedAsync(
        Guid subFlowInstanceId,
        CancellationToken cancellationToken = default);
}