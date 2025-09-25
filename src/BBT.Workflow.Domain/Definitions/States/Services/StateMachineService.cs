using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Instances;
using BBT.Workflow.Instances.Policies;
using BBT.Workflow.Scripting;
using BBT.Workflow.Validation;
using WorkflowExecutionContext = BBT.Workflow.Shared.ExecutionContext;

namespace BBT.Workflow.States;

/// <summary>
/// Provides state machine operations for workflow transitions and state management.
/// This service handles transition validation, rule execution, schema validation, and state navigation.
/// </summary>
/// <param name="stateTransitionPolicy">Policy for controlling state transition permissions and validation</param>
/// <param name="schemaValidator">Validator for JSON schema validation against transition data</param>
/// <param name="componentCacheStore">Cache store for retrieving workflow components like schemas</param>
/// <param name="ruleExecutionService">Service for executing business rules associated with transitions</param>
public class StateMachineService(
    StateTransitionPolicy stateTransitionPolicy,
    IJsonSchemaValidator schemaValidator,
    IComponentCacheStore componentCacheStore,
    IRuleExecutionService ruleExecutionService) : IStateMachineService
{
    /// <summary>
    /// Retrieves and validates a transition for a workflow instance based on the specified transition key.
    /// </summary>
    /// <param name="workflow">The workflow definition containing states and transitions</param>
    /// <param name="instance">The current workflow instance</param>
    /// <param name="transitionKey">The unique identifier of the transition to execute</param>
    /// <param name="scriptContext">The script execution context for rule evaluation</param>
    /// <param name="data">Optional JSON data to validate against the transition's schema</param>
    /// <param name="executionContext"></param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>
    /// A <see cref="Transition"/> object representing the validated transition that can be executed.
    /// </returns>
    /// <exception cref="NotFoundTransitionException">
    /// Thrown when the specified transition key is not found in the current state or shared transitions.
    /// </exception>
    /// <exception cref="TransitionRuleFailedException">
    /// Thrown when the transition rule evaluation fails or returns false.
    /// </exception>
    /// <exception cref="ValidationException">
    /// Thrown when the provided data does not conform to the transition's JSON schema.
    /// </exception>
    /// <remarks>
    /// This method performs several validation steps:
    /// 1. Verifies the transition exists for the current state
    /// 2. Checks if the instance can execute the transition based on policies
    /// 3. Evaluates any business rules associated with the transition
    /// 4. Validates input data against the transition's schema if present
    /// </remarks>
    public async Task<Transition> GetTransitionAsync(
        Definitions.Workflow workflow,
        Instance instance,
        string transitionKey,
        ScriptContext scriptContext,
        JsonElement? data = null,
        WorkflowExecutionContext executionContext = WorkflowExecutionContext.User,
        CancellationToken cancellationToken = default)
    {
        var currentState = workflow.GetState(instance.GetCurrentState);
        
        var transition = workflow.FindTransition(transitionKey, currentState);
        if (transition == null)
        {
            throw new NotFoundTransitionException(transitionKey);
        }

        await ValidateAndProcessTransitionAsync(instance, currentState, transition, scriptContext, data, executionContext, cancellationToken);
        return transition;
    }

    /// <summary>
    /// Validates and processes a transition by checking permissions, rules, and schema validation.
    /// </summary>
    private async Task ValidateAndProcessTransitionAsync(
        Instance instance,
        State currentState,
        Transition transition,
        ScriptContext scriptContext,
        JsonElement? data,
        WorkflowExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        // Validate that the instance can execute this transition (includes authorization check)
        instance.CanExecuteTransition(transition, currentState, stateTransitionPolicy, executionContext);
        
        // Execute transition rules if present
        if (transition.Rule != null)
        {
            var ruleResult = await ruleExecutionService.ExecuteRuleAsync(transition.Rule, scriptContext, cancellationToken);
            if (!ruleResult)
            {
                throw new TransitionRuleFailedException(transition.Key, "Transition rule evaluation failed");
            }
        }
        
        // Validate data against schema if present
        if (transition.Schema != null)
        {
            var schema = await componentCacheStore.GetSchemaAsync(transition.Schema, cancellationToken);
            schemaValidator.Validate(schema.Schema, data);
        }
    }

    /// <summary>
    /// Gets all available transition keys that can be executed from the current state of a workflow instance.
    /// </summary>
    /// <param name="workflow">The workflow definition to query for available transitions</param>
    /// <param name="instance">The workflow instance to check the current state</param>
    /// <returns>
    /// A list of strings representing the keys of all transitions available from the current state,
    /// including both state-specific and shared transitions.
    /// </returns>
    /// <remarks>
    /// This method combines:
    /// - Direct transitions from the current state
    /// - Shared transitions that are available for the current state
    /// The returned keys can be used with <see cref="GetTransitionAsync"/> to execute specific transitions.
    /// </remarks>
    public List<string> AvailableTransitionKeys(Definitions.Workflow workflow, Instance instance)
    {
        var availableTransitionKeys = new List<string>();
        var currentState = workflow.GetState(instance.GetCurrentState);
        availableTransitionKeys.AddRange(currentState.TransitionKeys());
        availableTransitionKeys.AddRange(workflow.AvailableSharedTransitionKeys(currentState.Key));
        return availableTransitionKeys;
    }

    /// <summary>
    /// Retrieves all automatic transitions that can be executed from the current state without user intervention.
    /// </summary>
    /// <param name="workflow">The workflow definition containing the states and transitions</param>
    /// <param name="instance">The workflow instance to check for automatic transitions</param>
    /// <returns>
    /// An enumerable collection of <see cref="Transition"/> objects representing automatic transitions
    /// that are eligible for execution from the current state.
    /// </returns>
    /// <remarks>
    /// Automatic transitions are executed by the workflow engine without external triggers.
    /// These transitions typically represent system-driven state changes or background processes.
    /// The workflow engine may execute these transitions based on configured intervals or events.
    /// </remarks>
    public IEnumerable<Transition> GetAutomaticTransitions(Definitions.Workflow workflow, Instance instance)
    {
        var currentState = workflow.GetState(instance.GetCurrentState);
        return currentState.GetAutoTransitions();
    }
    
    /// <summary>
    /// Retrieves all scheduled transitions that are configured to execute at specific times from the current state.
    /// </summary>
    /// <param name="workflow">The workflow definition containing the states and transitions</param>
    /// <param name="instance">The workflow instance to check for scheduled transitions</param>
    /// <returns>
    /// An enumerable collection of <see cref="Transition"/> objects representing scheduled transitions
    /// that are configured to execute at predetermined times from the current state.
    /// </returns>
    /// <remarks>
    /// Scheduled transitions are time-based transitions that execute according to a defined schedule.
    /// These might include timeout transitions, deadline-driven state changes, or periodic workflows.
    /// The workflow scheduler uses this information to determine when to trigger these transitions.
    /// </remarks>
    public IEnumerable<Transition> GetScheduledTransitions(Definitions.Workflow workflow, Instance instance)
    {
        var currentState = workflow.GetState(instance.GetCurrentState);
        return currentState.GetScheduledTransitions();
    }

    /// <summary>
    /// Gets all available user-triggered transition keys that can be executed from the current state of a workflow instance.
    /// This method filters out automatic and scheduled transitions, returning only manual transitions that users can trigger.
    /// </summary>
    /// <param name="workflow">The workflow definition to query for available transitions</param>
    /// <param name="instance">The workflow instance to check the current state</param>
    /// <returns>
    /// A list of strings representing the keys of manual transitions available from the current state,
    /// including both state-specific and shared manual transitions.
    /// </returns>
    /// <remarks>
    /// This method filters out:
    /// - Automatic transitions (TriggerType.Automatic)
    /// - Scheduled transitions (TriggerType.Scheduled)
    /// And only returns Manual and Event transitions that can be triggered by users.
    /// </remarks>
    public List<string> AvailableUserTransitionKeys(Definitions.Workflow workflow, Instance instance)
    {
        var currentState = workflow.GetState(instance.GetCurrentState);
        
        // Get manual transitions from current state
        var manualTransitions = currentState.Transitions
            .Where(t => t.TriggerType == TriggerType.Manual || t.TriggerType == TriggerType.Event)
            .Select(t => t.Key)
            .ToList();
        
        // Get manual shared transitions
        var manualSharedTransitions = workflow.SharedTransitions
            .Where(t => t.AvailableIn.Contains(currentState.Key) && 
                       (t.TriggerType == TriggerType.Manual || t.TriggerType == TriggerType.Event))
            .Select(t => t.Key)
            .ToList();
        
        manualTransitions.AddRange(manualSharedTransitions);
        return manualTransitions;
    }
}