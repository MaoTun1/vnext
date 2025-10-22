using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Execution.Validation;
using BBT.Workflow.Telemetry;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BBT.Workflow.Execution.Handlers;

/// <summary>
/// Base class for transition handlers providing common functionality.
/// </summary>
public abstract class TransitionHandlerBase : ITransitionHandler
{
    protected readonly ILogger Logger;
    protected readonly ITransitionValidationService ValidationService;

    /// <summary>
    /// Initializes a new instance of the TransitionHandlerBase.
    /// </summary>
    /// <param name="logger">Logger instance for the derived handler.</param>
    /// <param name="validationService">Service for transition validation operations.</param>
    protected TransitionHandlerBase(
        ILogger logger,
        ITransitionValidationService validationService)
    {
        Logger = logger;
        ValidationService = validationService;
    }

    /// <inheritdoc />
    public abstract bool CanHandle(TriggerType triggerType);

    /// <inheritdoc />
    public async Task PreHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var handlerName = GetType().Name;
        
        Logger.HandlerPreHandleStarted(
            TelemetryConstants.Prefixes.Execution,
            handlerName,
            context.Trigger.ToString(),
            context.TransitionKey);

        // Create span for PreHandle
        using var activity = WorkflowActivitySource.Instance.StartActivity(
            TelemetryConstants.SpanNames.HandlerPreHandle,
            ActivityKind.Internal);
        
        activity?.SetTag(TelemetryConstants.TagNames.HandlerName, handlerName);
        activity?.SetTag(TelemetryConstants.TagNames.TriggerType, context.Trigger.ToString());

        // Check if derived handler supports Result-based validation (e.g., AutomaticTransitionHandler)
        var internalValidationResult = await PreValidateInternalAsync(context, cancellationToken);
        if (!internalValidationResult.IsSuccess)
        {
            sw.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, internalValidationResult.Error.Message);
            activity?.AddTag("error.code", internalValidationResult.Error.Code);
            
            Logger.HandlerPreHandleFailed(
                new Exception(internalValidationResult.Error.Message ?? internalValidationResult.Error.Code),
                TelemetryConstants.Prefixes.Execution,
                handlerName,
                context.Trigger.ToString(),
                context.TransitionKey);
            
            // For auto-transition condition not met, throw special exception that can be handled upstream
            throw new AutoTransitionConditionNotMetException();
        }

        try
        {
            await PreValidateAsync(context, cancellationToken);
            await PreProcessAsync(context, cancellationToken);
            
            sw.Stop();
            Logger.HandlerPreHandleCompleted(
                TelemetryConstants.Prefixes.Execution,
                handlerName,
                context.Trigger.ToString(),
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
                sw.Stop();
                activity?.RecordExceptionWithStatus(ex);
                
                Logger.HandlerPreHandleFailed(
                ex,
                TelemetryConstants.Prefixes.Execution,
                handlerName,
                context.Trigger.ToString(),
                context.TransitionKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PostHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var handlerName = GetType().Name;
        
        Logger.HandlerPostHandleStarted(
            TelemetryConstants.Prefixes.Execution,
            handlerName,
            context.Trigger.ToString(),
            context.TransitionKey);

        // Create span for PostHandle
        using var activity = WorkflowActivitySource.Instance.StartActivity(
            TelemetryConstants.SpanNames.HandlerPostHandle,
            ActivityKind.Internal);
        
        activity?.SetTag(TelemetryConstants.TagNames.HandlerName, handlerName);
        activity?.SetTag(TelemetryConstants.TagNames.TriggerType, context.Trigger.ToString());

        try
        {
            await PostProcessAsync(context, cancellationToken);
            await PostValidateAsync(context, cancellationToken);
            
            sw.Stop();
            Logger.HandlerPostHandleCompleted(
                TelemetryConstants.Prefixes.Execution,
                handlerName,
                context.Trigger.ToString(),
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
                sw.Stop();
                activity?.RecordExceptionWithStatus(ex);
                
                Logger.HandlerPostHandleFailed(
                ex,
                TelemetryConstants.Prefixes.Execution,
                handlerName,
                context.Trigger.ToString(),
                context.TransitionKey);
            throw;
        }
    }

    /// <summary>
    /// Performs Result-based pre-validation logic for handlers that support it (e.g., AutomaticTransitionHandler).
    /// Override in derived classes to implement Result-based validation.
    /// Default implementation returns Ok, allowing the handler to use exception-based validation if needed.
    /// </summary>
    /// <param name="context">The transition execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>Result indicating validation success or failure.</returns>
    protected virtual Task<Result> PreValidateInternalAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        // Default: no Result-based validation, proceed with exception-based validation
        return Task.FromResult(Result.Ok());
    }

    /// <summary>
    /// Performs pre-validation logic specific to the trigger type using Result Pattern.
    /// This method provides common validation logic and can be overridden in derived classes for specific validation rules.
    /// Throws an exception if validation fails to maintain backward compatibility with exception-based pipeline.
    /// </summary>
    /// <param name="context">The transition execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="WorkflowValidationException">Thrown when validation fails with detailed error information</exception>
    protected virtual async Task PreValidateAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        Logger.LogTrace("Performing common pre-validation for transition {TransitionKey}", context.TransitionKey);

        // Perform common validation using the validation service (Result pattern)
        var validationResult = await ValidationService.ValidateAsync(context, cancellationToken);
        
        if (!validationResult.IsSuccess)
        {
            Logger.LogTrace("Common pre-validation failed for transition {TransitionKey}: {ErrorCode} - {ErrorMessage}",
                context.TransitionKey, validationResult.Error.Code, validationResult.Error.Message);
            
            // Convert Result to exception for pipeline compatibility
            throw new WorkflowValidationException(validationResult.Error);
        }

        Logger.LogTrace("Common pre-validation completed for transition {TransitionKey}", context.TransitionKey);
    }

    /// <summary>
    /// Performs pre-processing logic specific to the trigger type.
    /// Override in derived classes to implement specific pre-processing logic.
    /// </summary>
    /// <param name="context">The transition execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual Task PreProcessAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs post-processing logic specific to the trigger type.
    /// Override in derived classes to implement specific post-processing logic.
    /// </summary>
    /// <param name="context">The transition execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual Task PostProcessAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs post-validation logic specific to the trigger type.
    /// Override in derived classes to implement specific post-validation rules.
    /// </summary>
    /// <param name="context">The transition execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual Task PostValidateAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
