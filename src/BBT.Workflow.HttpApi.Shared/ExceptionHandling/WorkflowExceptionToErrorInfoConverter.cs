using BBT.Aether.ExceptionHandling;
using BBT.Aether.Http;

namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Custom exception to error info converter for workflow-specific exceptions.
/// Extends the default Aether exception converter to handle workflow domain exceptions
/// such as WorkflowValidationException with detailed validation errors.
/// </summary>
/// <param name="serviceProvider">Service provider for resolving dependencies</param>
public class WorkflowExceptionToErrorInfoConverter(IServiceProvider serviceProvider)
    : DefaultExceptionToErrorInfoConverter(serviceProvider)
{
    /// <summary>
    /// Creates error information from exceptions without predefined error codes.
    /// Handles workflow-specific exceptions by converting them to appropriate ServiceErrorInfo objects
    /// while maintaining their original error details, codes, and additional data.
    /// For WorkflowValidationException, includes detailed field-level validation errors in the response.
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
            WorkflowValidationException validationException => EnrichWithValidationErrors(errorInfo, validationException),
            _ => errorInfo
        };
    }

    /// <summary>
    /// Enriches the ServiceErrorInfo with validation errors from WorkflowValidationException.
    /// Adds detailed field-level validation errors to the Data dictionary for client consumption.
    /// </summary>
    /// <param name="errorInfo">The base error information</param>
    /// <param name="exception">The workflow validation exception containing validation errors</param>
    /// <returns>Enriched ServiceErrorInfo with validation errors</returns>
    private static ServiceErrorInfo EnrichWithValidationErrors(
        ServiceErrorInfo errorInfo, 
        WorkflowValidationException exception)
    {
        if (exception.ValidationErrors is { Count: > 0 })
        {
            errorInfo.Data["errors"] = exception.ValidationErrors
                .Select(ve => new
                {
                    field = ve.MemberNames.FirstOrDefault() ?? string.Empty,
                    message = ve.ErrorMessage
                })
                .ToList();
        }

        // Also update target if available from the Error
        if (!string.IsNullOrEmpty(exception.Target))
        {
            errorInfo.Data["target"] = exception.Target;
        }

        return errorInfo;
    }
}