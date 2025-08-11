using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Provides a base implementation for workflow task executors.
/// This abstract class encapsulates common functionality and dependencies required by task executors.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling and executing workflow scripts.</param>
/// <param name="logger">The logger instance for logging task execution information and errors.</param>
public abstract class TaskExecutor(
    IScriptEngine scriptEngine,
    ILogger logger)
{
    /// <summary>
    /// Gets the script engine instance used for compiling and executing workflow scripts.
    /// This engine handles the compilation of script code into executable instances.
    /// </summary>
    protected IScriptEngine ScriptEngine { get; } = scriptEngine;
    
    /// <summary>
    /// Gets the logger instance for logging task execution information and errors.
    /// </summary>
    protected ILogger Logger { get; } = logger;
    
    protected virtual async Task<ScriptResponse> PrepareInputAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Starting input preparation for task {TaskKey} of type {TaskType}", task.Key, task.GetType().Name);
        
        try
        {
            var scriptRunner = await ScriptEngine.CompileToInstanceAsync<IMapping>(
                scriptCode, 
                cancellationToken: cancellationToken);
            
            Logger.LogDebug("Script compiled successfully for input preparation on task {TaskKey}", task.Key);
            
            var response = await scriptRunner.InputHandler(task, context);
            
            Logger.LogDebug("Input preparation completed successfully for task {TaskKey}", task.Key);
            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to prepare input for task {TaskKey} of type {TaskType}", task.Key, task.GetType().Name);
            throw;
        }
    }

    protected virtual async Task<ScriptResponse> ProcessOutputAsync(
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Starting output processing with script code");
        
        try
        {
            var scriptRunner = await ScriptEngine.CompileToInstanceAsync<IMapping>(
                scriptCode, 
                cancellationToken: cancellationToken);
            
            Logger.LogDebug("Script compiled successfully for output processing");
            
            var response = await scriptRunner.OutputHandler(context);
            
            Logger.LogDebug("Output processing completed successfully");
            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process output during script execution");
            throw;
        }
    }

    /// <summary>
    /// Creates a standardized successful task response.
    /// </summary>
    protected virtual StandardTaskResponse CreateSuccessResponse(
        object? data, 
        string taskType,
        long? executionDurationMs = null,
        int? statusCode = null, 
        Dictionary<string, string>? headers = null,
        Dictionary<string, object>? metadata = null)
    {
        Logger.LogInformation("Creating success response for task type {TaskType} with execution duration {ExecutionDurationMs}ms", 
            taskType, executionDurationMs);
        
        return new StandardTaskResponse
        {
            Data = data,
            IsSuccess = true,
            StatusCode = statusCode,
            Headers = headers,
            Metadata = metadata,
            ExecutionDurationMs = executionDurationMs,
            TaskType = taskType
        };
    }

    /// <summary>
    /// Creates a standardized error task response.
    /// </summary>
    protected virtual StandardTaskResponse CreateErrorResponse(
        string errorMessage, 
        string taskType,
        long? executionDurationMs = null,
        int? statusCode = null,
        Exception? exception = null,
        Dictionary<string, object>? metadata = null)
    {
        Logger.LogError(exception, "Creating error response for task type {TaskType} with message: {ErrorMessage}, execution duration: {ExecutionDurationMs}ms", 
            taskType, errorMessage, executionDurationMs);
        
        var response = new StandardTaskResponse
        {
            Data = null,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            StatusCode = statusCode,
            ExecutionDurationMs = executionDurationMs,
            TaskType = taskType,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        if (exception != null)
        {
            response.Metadata["ExceptionType"] = exception.GetType().Name;
            response.Metadata["StackTrace"] = exception.StackTrace ?? string.Empty;
        }

        return response;
    }
}