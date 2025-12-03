using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.Validation;
using Microsoft.Extensions.Logging;
using BBT.Aether.Results;
using BBT.Aether.Validation;

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
        // Skip all PreHandle operations for Resume mode - validations already done
        if (context.Directives.IsSubFlowResume)
        {
            return;
        }
        
        await PreValidateAsync(context, cancellationToken);
        await PreProcessAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PostHandleAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        await PostProcessAsync(context, cancellationToken);
        await PostValidateAsync(context, cancellationToken);
    }

    /// <summary>
    /// Performs pre-validation logic specific to the trigger type using Result Pattern.
    /// This method provides common validation logic and can be overridden in derived classes for specific validation rules.
    /// Throws an exception if validation fails to maintain backward compatibility with exception-based pipeline.
    /// </summary>
    /// <param name="context">The transition execution context.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="AetherValidationException">Thrown when validation fails with detailed error information</exception>
    protected virtual async Task PreValidateAsync(TransitionExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Perform common validation using the validation service (Result pattern)
        var validationResult = await ValidationService.ValidateAsync(context, cancellationToken);
        validationResult.ThrowIfFailure();
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