using BBT.Workflow.Caching;
using BBT.Workflow.Domain;
using BBT.Workflow.Instances.Policies;
using BBT.Workflow.Shared;
using BBT.Workflow.Validation;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Validation;

/// <summary>
/// Provides transition validation operations within the execution pipeline using Result Pattern.
/// This service handles transition validation, rule execution, schema validation, and policy checks without throwing exceptions.
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
    public async Task<Result> ValidateAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Validating transition {TransitionKey} for instance {InstanceId}",
            context.TransitionKey, context.InstanceId);

        // 1. Validate data against schema if present
        var schemaResult = await ValidateTransitionSchemaAsync(context, cancellationToken);
        if (!schemaResult.IsSuccess)
        {
            logger.LogWarning("Transition schema validation failed for {TransitionKey} on instance {InstanceId}: {ErrorCode}",
                context.TransitionKey, context.InstanceId, schemaResult.Error.Code);
            return schemaResult;
        }

        logger.LogDebug("Transition validation completed successfully for {TransitionKey} on instance {InstanceId}",
            context.TransitionKey, context.InstanceId);
        
        // 2. Validate that the instance can execute this transition (includes authorization check)
        var policyResult = await ValidateTransitionPolicyAsync(context, context.Actor, cancellationToken);
        if (!policyResult.IsSuccess)
        {
            logger.LogWarning("Transition policy validation failed for {TransitionKey} on instance {InstanceId}: {ErrorCode} - {ErrorMessage}",
                context.TransitionKey, context.InstanceId, policyResult.Error.Code, policyResult.Error.Message);
            return policyResult;
        }
        
        return Result.Ok();
    }

    /// <summary>
    /// Validates transition policies and authorization using Result Pattern.
    /// </summary>
    private Task<Result> ValidateTransitionPolicyAsync(
        TransitionExecutionContext context,
        ExecutionActor executionActor,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("Validating transition policy for {TransitionKey}", context.TransitionKey);
        
        if (context.Instance.HasActiveSubFlow)
        {
            logger.LogTrace("Skipping transition policy validation for {TransitionKey} - instance has active subflow", 
                context.TransitionKey);
            return Task.FromResult(Result.Ok());
        }

        var result = context.Instance.CanExecuteTransition(
            context.Transition!, 
            context.Current, 
            stateTransitionPolicy, 
            executionActor);
        
        if (result.IsSuccess)
        {
            logger.LogTrace("Transition policy validation passed for {TransitionKey}", context.TransitionKey);
        }
        
        return Task.FromResult(result);
    }

    /// <summary>
    /// Validates transition data against JSON schema using Result Pattern.
    /// </summary>
    private async Task<Result> ValidateTransitionSchemaAsync(
        TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.Transition?.Schema == null)
        {
            logger.LogTrace("No schema to validate for transition {TransitionKey}", context.TransitionKey);
            return Result.Ok();
        }

        logger.LogTrace("Validating transition schema for {TransitionKey}", context.TransitionKey);

        var schema = await componentCacheStore.GetSchemaAsync(context.Transition.Schema, cancellationToken);
        
        var validationResult = schemaValidator.Validate(schema.Schema, context.DataElement);
        
        if (!validationResult.IsSuccess)
        {
            // Enhance the error with transition-specific information
            return Result.Fail(
                WorkflowErrors.SchemaValidationFailed(
                    context.TransitionKey, 
                    validationResult.Error.ValidationErrors ?? []));
        }

        logger.LogTrace("Transition schema validation passed for {TransitionKey}", context.TransitionKey);
        return Result.Ok();
    }
}
