using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Execution.StateMachine;

/// <summary>
/// The main executor class that manages and executes state transitions in the workflow state machine.
/// This class coordinates transitions between states for workflow instances and executes related tasks.
/// </summary>
/// <remarks>
/// StateMachineExecutor performs the following operations:
/// <list type="bullet">
/// <item>Executes transition OnExecution tasks</item>
/// <item>Executes current state's OnExit tasks</item>
/// <item>Executes target state's OnEntry tasks</item>
/// <item>Changes the instance's state</item>
/// <item>Checks for automatic and scheduled transitions</item>
/// <item>Manages workflow timeouts</item>
/// <item>Handles SubFlow and SubProcess workflows</item>
/// </list>
/// </remarks>
public interface IStateMachineExecutor
{
    /// <summary>
    /// Asynchronously executes a workflow transition using the specified script context.
    /// This method manages the complete lifecycle of the transition and performs the state change.
    /// </summary>
    /// <param name="context">
    /// The script context containing all information related to the transition to be executed.
    /// Includes instance, workflow, transition, body, and header information.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token used to cancel the operation. Default value is default.
    /// </param>
    /// <returns>
    /// A Task representing the transition execution operation. The task completes when the operation is finished.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the context parameter is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the specified state or transition cannot be found in the workflow.
    /// </exception>
    /// <remarks>
    /// This method performs the following operations in order:
    /// <list type="number">
    /// <item>Creates an InstanceTransition record and saves it to the database</item>
    /// <item>Executes the transition's OnExecution tasks</item>
    /// <item>Executes the current state's OnExit tasks</item>
    /// <item>Executes the target state's OnEntry tasks</item>
    /// <item>Changes the instance's state</item>
    /// <item>Handles SubFlow/SubProcess workflows if applicable</item>
    /// <item>Checks for scheduled and automatic transitions</item>
    /// <item>Updates instance status and completes the transition record</item>
    /// </list>
    /// </remarks>
    Task ExecuteTransitionAsync(
        ScriptContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously initiates the timeout process for the specified workflow and sets up the timer.
    /// If the workflow has a defined timeout duration, it enqueues a background job to terminate 
    /// the workflow after the specified time period.
    /// </summary>
    /// <param name="workflow">
    /// The workflow definition for which the timeout operation will be applied. 
    /// The timeout configuration must be defined within this workflow.
    /// </param>
    /// <param name="instance">
    /// The workflow instance for which the timeout operation will be applied. 
    /// The timeout timer will be started for this instance.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token used to cancel the operation. Default value is default.
    /// </param>
    /// <returns>
    /// A Task representing the timeout scheduling operation. The task completes when the operation is finished.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the workflow or instance parameter is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method only performs operations if a timeout is defined in the workflow. 
    /// If no timeout is defined, no operation is performed.
    /// </para>
    /// <para>
    /// When the timeout period expires, a workflow termination process is initiated through the background job service.
    /// This process checks the current state of the workflow and terminates it appropriately.
    /// </para>
    /// </remarks>
    Task FlowTimeoutAsync(
        Definitions.Workflow workflow,
        Instance instance,
        CancellationToken cancellationToken = default);
}