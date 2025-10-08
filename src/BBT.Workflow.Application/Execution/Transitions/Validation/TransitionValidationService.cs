using System.Text.Json;
using BBT.Workflow.Caching;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Instances.Policies;
using BBT.Workflow.Scripting;
using BBT.Workflow.Shared;
using BBT.Workflow.Tasks;
using BBT.Workflow.Validation;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Validation;

/// <summary>
/// Provides transition validation operations within the execution pipeline.
/// This service handles transition validation, rule execution, schema validation, and policy checks.
/// </summary>
/// <param name="stateTransitionPolicy">Policy for controlling state transition permissions and validation</param>
/// <param name="schemaValidator">Validator for JSON schema validation against transition data</param>
/// <param name="componentCacheStore">Cache store for retrieving workflow components like schemas</param>
/// <param name="logger">Logger for validation operations</param>
public class TransitionValidationService(
    StateTransitionPolicy stateTransitionPolicy,
    IJsonSchemaValidator schemaValidator,
    IComponentCacheStore componentCacheStore,
    ILogger<TransitionValidationService> logger) : ITransitionValidationService
{
    /// <inheritdoc />
    public async Task ValidateAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Validating transition {TransitionKey} for instance {InstanceId}",
            context.TransitionKey, context.InstanceId);

        try
        {
            // 1. Validate that the instance can execute this transition (includes authorization check)
            await ValidateTransitionPolicyAsync(context, context.Actor, cancellationToken);
            
            // 2. Validate data against schema if present
            await ValidateTransitionSchemaAsync(context, cancellationToken);

            logger.LogDebug("Transition validation completed successfully for {TransitionKey} on instance {InstanceId}",
                context.TransitionKey, context.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transition validation failed for {TransitionKey} on instance {InstanceId}",
                context.TransitionKey, context.InstanceId);
            throw;
        }
    }

    /// <summary>
    /// Validates transition policies and authorization.
    /// </summary>
    private async Task ValidateTransitionPolicyAsync(
        TransitionExecutionContext context,
        ExecutionActor executionActor,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("Validating transition policy for {TransitionKey}", context.TransitionKey);
        
        // Use the existing policy validation logic from StateMachineService
        context.Instance.CanExecuteTransition(context.Transition, context.Current, stateTransitionPolicy, executionActor);
        
        await Task.CompletedTask; // Keep async signature for future enhancements
        
        logger.LogTrace("Transition policy validation passed for {TransitionKey}", context.TransitionKey);
    }

    /// <summary>
    /// Validates transition data against JSON schema.
    /// </summary>
    private async Task ValidateTransitionSchemaAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.Transition.Schema == null)
        {
            logger.LogTrace("No schema to validate for transition {TransitionKey}", context.TransitionKey);
            return;
        }

        logger.LogTrace("Validating transition schema for {TransitionKey}", context.TransitionKey);

        var schema = await componentCacheStore.GetSchemaAsync(context.Transition.Schema, cancellationToken);
        
        // Convert context.Data to JsonElement if needed
        JsonElement? dataElement = context.Data switch
        {
            JsonElement element => element,
            string jsonString => JsonSerializer.Deserialize<JsonElement>(jsonString),
            null => null,
            _ => JsonSerializer.SerializeToElement(context.Data)
        };

        schemaValidator.Validate(schema.Schema, dataElement);

        logger.LogTrace("Transition schema validation passed for {TransitionKey}", context.TransitionKey);
    }
}
