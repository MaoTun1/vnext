using BBT.Aether.ExceptionHandling;
using BBT.Aether.Http;

namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Custom exception to error info converter for workflow-specific exceptions.
/// Extends the default Aether exception converter to handle workflow domain exceptions
/// such as ConflictException, NotFoundDomainException, and RuntimeSchemaInvalidException.
/// </summary>
/// <param name="serviceProvider">Service provider for resolving dependencies</param>
public class WorkflowExceptionToErrorInfoConverter(IServiceProvider serviceProvider)
    : DefaultExceptionToErrorInfoConverter(serviceProvider)
{
    /// <summary>
    /// Creates error information from exceptions without predefined error codes.
    /// Handles workflow-specific exceptions by converting them to appropriate ServiceErrorInfo objects
    /// while maintaining their original error details, codes, and additional data.
    /// </summary>
    /// <param name="exception">The exception to convert to error information</param>
    /// <param name="options">Exception handling options from Aether framework</param>
    /// <returns>A ServiceErrorInfo object containing structured error information</returns>
    protected override ServiceErrorInfo CreateErrorInfoWithoutCode(Exception exception,
        AetherExceptionHandlingOptions options)
    {
        var errorInfo = base.CreateErrorInfoWithoutCode(exception, options);
        return exception switch
        {
            ConflictException ex => new ServiceErrorInfo(ex.Message, ex.Details, ex.Code, ex.Data),
            NotFoundDomainException ex => new ServiceErrorInfo(ex.Message, ex.Details, ex.Code, ex.Data),
            RuntimeSchemaInvalidException ex => new ServiceErrorInfo(ex.Message, ex.Details, ex.Code, ex.Data),
            SubFlowBlockedException ex => new ServiceErrorInfo(ex.Message, ex.Details, ex.Code, ex.Data),
            InstanceNotFoundException ex => new ServiceErrorInfo(ex.Message, ex.Details, ex.Code, ex.Data),
            NotFoundStateException ex => new ServiceErrorInfo(ex.Message, ex.Details, ex.Code, ex.Data),
            InvalidStateException ex => new ServiceErrorInfo(ex.Message, ex.Details, ex.Code, ex.Data),
            NotFoundTransitionException ex => new ServiceErrorInfo(ex.Message, ex.Details, ex.Code, ex.Data),
            TransitionRuleFailedException ex => new ServiceErrorInfo(ex.Message, ex.Details, ex.Code, ex.Data),
            _ => errorInfo
        };
    }
}