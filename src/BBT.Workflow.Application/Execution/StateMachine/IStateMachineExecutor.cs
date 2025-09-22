using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using WorkflowExecutionContext = BBT.Workflow.Shared.ExecutionContext;

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

    /// <summary>
    /// Asynchronously schedules workflow transitions for later execution based on their timing configurations.
    /// This method identifies transitions that are configured to be executed at specific times or delays
    /// and enqueues them as background jobs for future execution.
    /// </summary>
    /// <param name="workflow">
    /// The workflow definition containing transitions that may need to be scheduled.
    /// The method examines all transitions within this workflow for scheduling requirements.
    /// </param>
    /// <param name="instance">
    /// The workflow instance for which transitions will be scheduled.
    /// The scheduling context will be associated with this specific instance.
    /// </param>
    /// <param name="context"></param>
    /// <param name="cancellationToken">
    /// The cancellation token used to cancel the operation. Default value is default.
    /// </param>
    /// <returns>
    /// A Task representing the scheduling operation. The task completes when all applicable 
    /// transitions have been scheduled for later execution.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the workflow or instance parameter is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method processes transitions that have timing-based execution requirements such as:
    /// </para>
    /// <list type="bullet">
    /// <item>Delayed transitions (execute after a specific time period)</item>
    /// <item>Scheduled transitions (execute at a specific date/time)</item>
    /// <item>Recurring transitions (execute periodically)</item>
    /// </list>
    /// <para>
    /// The scheduled transitions are queued as background jobs and will be executed 
    /// when their scheduled time arrives.
    /// </para>
    /// </remarks>
    Task ScheduleTransitionsForLaterExecutionAsync(
        Definitions.Workflow workflow,
        Instance instance,
        ScriptContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks for and executes automatic transitions that are eligible for immediate execution.
    /// This method evaluates conditions and triggers for automatic transitions and executes them 
    /// if their criteria are met.
    /// </summary>
    /// <param name="workflow">
    /// The workflow definition containing automatic transitions to be evaluated.
    /// The method examines all automatic transitions within this workflow.
    /// </param>
    /// <param name="instance">
    /// The workflow instance for which automatic transitions will be evaluated and executed.
    /// The current state and context of this instance will be used for condition evaluation.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token used to cancel the operation. Default value is default.
    /// </param>
    /// <returns>
    /// A Task representing the automatic transition evaluation and execution operation. 
    /// The task completes when all eligible automatic transitions have been processed.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the workflow or instance parameter is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method processes automatic transitions that meet the following criteria:
    /// </para>
    /// <list type="bullet">
    /// <item>Transitions configured for automatic execution</item>
    /// <item>Transitions whose conditions are satisfied</item>
    /// <item>Transitions that are valid from the current state</item>
    /// <item>Transitions that have no timing constraints or whose timing has been met</item>
    /// </list>
    /// <para>
    /// Automatic transitions are executed immediately without external triggers, 
    /// allowing workflows to progress automatically based on predefined rules and conditions.
    /// </para>
    /// <para>
    /// This method may result in multiple state transitions if multiple automatic 
    /// transitions are eligible for execution in sequence.
    /// </para>
    /// </remarks>
    Task CheckAndExecuteAutomaticTransitionsAsync(
        Definitions.Workflow workflow,
        Instance instance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously executes the start transition for a workflow instance.
    /// This method handles only the state machine logic, expecting the instance to be already prepared.
    /// </summary>
    /// <param name="workflow">The workflow definition containing the start transition</param>
    /// <param name="instance">The prepared instance to start</param>
    /// <param name="attributes">Initial data attributes for the instance</param>
    /// <param name="headers">Request headers for the start transition</param>
    /// <param name="routeValues">Route values for the start transition</param>
    /// <param name="executionContext">The execution context (User or System)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the start transition execution</returns>
    /// <remarks>
    /// This method performs:
    /// 1. Instance status validation and activation if needed
    /// 2. Start transition execution
    /// 3. Instance persistence
    /// 4. Flow timeout scheduling
    /// </remarks>
    Task ExecuteInstanceStartAsync(
        Definitions.Workflow workflow,
        Instance instance,
        JsonElement? attributes,
        Dictionary<string, string>? headers,
        Dictionary<string, object?>? routeValues,
        WorkflowExecutionContext executionContext = WorkflowExecutionContext.User,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously executes a manual transition for an existing workflow instance.
    /// This method handles the complete transition lifecycle with proper state management.
    /// </summary>
    /// <param name="workflow">The workflow definition containing the transition</param>
    /// <param name="instance">The instance to execute the transition for</param>
    /// <param name="transitionKey">The key of the transition to execute</param>
    /// <param name="data">Optional data to pass to the transition</param>
    /// <param name="headers">Request headers for the transition</param>
    /// <param name="routeValues">Route values for the transition</param>
    /// <param name="executionContext">The execution context (User or System)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the transition execution</returns>
    /// <remarks>
    /// This method performs:
    /// 1. Transition validation and preparation
    /// 2. Data versioning and persistence
    /// 3. Transition execution with full lifecycle
    /// 4. Automatic transition checking
    /// 5. Instance status management
    /// </remarks>
    Task ExecuteManualTransitionAsync(
        Definitions.Workflow workflow,
        Instance instance,
        string transitionKey,
        JsonElement? data,
        Dictionary<string, string>? headers,
        Dictionary<string, object?>? routeValues,
        WorkflowExecutionContext executionContext = WorkflowExecutionContext.User,
        CancellationToken cancellationToken = default);
}