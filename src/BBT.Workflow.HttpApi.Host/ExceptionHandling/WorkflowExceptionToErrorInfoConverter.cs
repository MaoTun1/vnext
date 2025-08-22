using BBT.Aether.ExceptionHandling;
using BBT.Aether.Http;
using BBT.Workflow.Monitoring;

namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Custom exception to error info converter for workflow-specific exceptions.
/// Extends the default Aether exception converter to handle workflow domain exceptions
/// such as ConflictException, NotFoundDomainException, and RuntimeSchemaInvalidException.
/// Records metrics for different types of workflow exceptions and errors.
/// </summary>
/// <param name="serviceProvider">Service provider for resolving dependencies</param>
/// <param name="workflowMetrics">Workflow metrics service for recording error metrics</param>
public class WorkflowExceptionToErrorInfoConverter(
    IServiceProvider serviceProvider,
    IWorkflowMetrics workflowMetrics)
    : DefaultExceptionToErrorInfoConverter(serviceProvider)
{
    /// <summary>
    /// Creates error information from exceptions without predefined error codes.
    /// Handles workflow-specific exceptions by converting them to appropriate ServiceErrorInfo objects
    /// while maintaining their original error details, codes, and additional data.
    /// Records metrics for different types of workflow exceptions and errors.
    /// </summary>
    /// <param name="exception">The exception to convert to error information</param>
    /// <param name="options">Exception handling options from Aether framework</param>
    /// <returns>A ServiceErrorInfo object containing structured error information</returns>
    protected override ServiceErrorInfo CreateErrorInfoWithoutCode(Exception exception,
        AetherExceptionHandlingOptions options)
    {
        var errorInfo = base.CreateErrorInfoWithoutCode(exception, options);
        
        // Record workflow error metrics based on exception type
        RecordExceptionMetrics(exception);
        
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

    /// <summary>
    /// Records metrics for different types of workflow exceptions.
    /// </summary>
    /// <param name="exception">The exception to record metrics for</param>
    private void RecordExceptionMetrics(Exception exception)
    {
        const string component = "ExceptionHandler";
        var exceptionType = exception.GetType().Name;
        
        switch (exception)
        {
            case ConflictException:
                workflowMetrics.RecordWorkflowError("business", "medium", component);
                workflowMetrics.RecordWorkflowException(exceptionType, component, "ConflictResolution");
                break;
                
            case NotFoundDomainException:
            case InstanceNotFoundException:
            case NotFoundStateException:
                workflowMetrics.RecordWorkflowError("business", "low", component);
                workflowMetrics.RecordWorkflowException(exceptionType, component, "EntityNotFound");
                break;
                
            case RuntimeSchemaInvalidException:
                workflowMetrics.RecordWorkflowError("system", "high", component);
                workflowMetrics.RecordWorkflowException(exceptionType, component, "SchemaValidation");
                break;
                
            case SubFlowBlockedException:
                workflowMetrics.RecordWorkflowError("business", "medium", component);
                workflowMetrics.RecordWorkflowException(exceptionType, component, "SubFlowExecution");
                break;
                
            case InvalidStateException:
            case NotFoundTransitionException:
            case TransitionRuleFailedException:
                workflowMetrics.RecordWorkflowError("business", "medium", component);
                workflowMetrics.RecordWorkflowException(exceptionType, component, "StateMachine");
                break;
                
            default:
                // Record unhandled exceptions
                workflowMetrics.RecordWorkflowError("system", "high", component);
                workflowMetrics.RecordWorkflowException(exceptionType, component, "UnhandledException");
                break;
        }
    }
}