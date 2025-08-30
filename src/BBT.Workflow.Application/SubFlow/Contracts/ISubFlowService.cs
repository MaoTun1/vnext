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
    /// Checks if transition should be forwarded to SubFlow instance.
    /// If SubFlow is active, forwards the transition to SubFlow instance via remote call.
    /// Returns true if transition was forwarded, false if should be processed locally.
    /// </summary>
    /// <param name="instanceId">The main instance ID</param>
    /// <param name="transitionKey">The transition key to execute</param>
    /// <param name="input">The transition input data</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>True if transition was forwarded to SubFlow, false if should be processed locally</returns>
    Task<bool> TryForwardTransitionToSubFlowAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default);
}