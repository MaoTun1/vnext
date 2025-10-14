using BBT.Workflow.Definitions;
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
    /// Performs pre-validation logic specific to the trigger type.
    /// This method provides common validation logic and can be overridden in derived classes for specific validation rules.
    /// </summary>
    /// <param name="context">The transition execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual async Task PreValidateAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        Logger.LogTrace("Performing common pre-validation for transition {TransitionKey}", context.TransitionKey);

        // Perform common validation using the validation service
        await ValidationService.ValidateAsync(
            context,
            cancellationToken);

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
