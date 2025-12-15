using BBT.Aether;
using BBT.Aether.Http;

namespace BBT.Workflow.ExceptionHandling;

/// <summary>
/// Exception thrown when a remote service call returns an error with Aether error format.
/// This exception preserves the structured error information from the remote service
/// and provides access to both the ServiceErrorInfo and HTTP status code.
/// </summary>
public class RemoteServiceException : AetherException
{
    /// <summary>
    /// The structured error information from the remote service
    /// </summary>
    public ServiceErrorInfo ErrorInfo { get; }
    
    /// <summary>
    /// The HTTP status code returned by the remote service
    /// </summary>
    public int HttpStatusCode { get; }

    /// <summary>
    /// Initializes a new instance of the RemoteServiceException class
    /// </summary>
    /// <param name="message">The exception message</param>
    /// <param name="errorInfo">The structured error information from the remote service</param>
    /// <param name="httpStatusCode">The HTTP status code</param>
    public RemoteServiceException(string message, ServiceErrorInfo errorInfo, int httpStatusCode) 
        : base(message)
    {
        ErrorInfo = errorInfo;
        HttpStatusCode = httpStatusCode;
    }

    /// <summary>
    /// Initializes a new instance of the RemoteServiceException class with an inner exception
    /// </summary>
    /// <param name="message">The exception message</param>
    /// <param name="errorInfo">The structured error information from the remote service</param>
    /// <param name="httpStatusCode">The HTTP status code</param>
    /// <param name="innerException">The inner exception that caused this exception</param>
    public RemoteServiceException(string message, ServiceErrorInfo errorInfo, int httpStatusCode, Exception innerException) 
        : base(message, innerException)
    {
        ErrorInfo = errorInfo;
        HttpStatusCode = httpStatusCode;
    }
}