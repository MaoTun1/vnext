using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace BBT.Workflow.Telemetry;

/// <summary>
/// Provides helper methods for creating structured logging scopes with automatic enrichment.
/// Scopes ensure that all logs within a context carry consistent metadata.
/// </summary>
public static class LogScope
{
    /// <summary>
    /// Creates a logging scope for transition execution with automatic metadata enrichment.
    /// </summary>
    /// <param name="logger">The logger to create the scope on</param>
    /// <param name="domain">The workflow domain (e.g., "amorphie")</param>
    /// <param name="flow">The workflow flow name (e.g., "loan-approval")</param>
    /// <param name="flowVersion">The workflow flow version (e.g., "v1.0", "1")</param>
    /// <param name="instanceId">The instance ID being processed</param>
    /// <param name="transitionKey">The transition key being executed</param>
    /// <param name="method">Auto-captured: the calling method name</param>
    /// <param name="file">Auto-captured: the source file path</param>
    /// <returns>A disposable scope object</returns>
    public static IDisposable? ForTransition(
        this ILogger logger,
        string domain,
        string flow,
        string? flowVersion,
        Guid instanceId,
        string transitionKey,
        [CallerMemberName] string? method = null,
        [CallerFilePath] string? file = null)
    {
        var className = Path.GetFileNameWithoutExtension(file ?? "Unknown");
        
        var state = new Dictionary<string, object?>
        {
            [TelemetryConstants.ScopeFields.Domain] = domain,
            [TelemetryConstants.ScopeFields.Flow] = flow,
            [TelemetryConstants.ScopeFields.FlowVersion] = flowVersion,
            [TelemetryConstants.ScopeFields.InstanceId] = instanceId,
            [TelemetryConstants.ScopeFields.TransitionKey] = transitionKey,
            [TelemetryConstants.ScopeFields.Method] = method,
            [TelemetryConstants.ScopeFields.Class] = className
        };
        
        return logger.BeginScope(state);
    }

    /// <summary>
    /// Creates a logging scope for instance execution with automatic metadata enrichment.
    /// </summary>
    /// <param name="logger">The logger to create the scope on</param>
    /// <param name="domain">The workflow domain</param>
    /// <param name="flow">The workflow flow name</param>
    /// <param name="flowVersion">The workflow flow version (e.g., "v1.0", "1")</param>
    /// <param name="instanceId">The instance ID being processed</param>
    /// <param name="method">Auto-captured: the calling method name</param>
    /// <param name="file">Auto-captured: the source file path</param>
    /// <returns>A disposable scope object</returns>
    public static IDisposable? ForInstance(
        this ILogger logger,
        string domain,
        string flow,
        string? flowVersion,
        Guid instanceId,
        [CallerMemberName] string? method = null,
        [CallerFilePath] string? file = null)
    {
        var className = Path.GetFileNameWithoutExtension(file ?? "Unknown");
        
        var state = new Dictionary<string, object?>
        {
            [TelemetryConstants.ScopeFields.Domain] = domain,
            [TelemetryConstants.ScopeFields.Flow] = flow,
            [TelemetryConstants.ScopeFields.FlowVersion] = flowVersion,
            [TelemetryConstants.ScopeFields.InstanceId] = instanceId,
            [TelemetryConstants.ScopeFields.Method] = method,
            [TelemetryConstants.ScopeFields.Class] = className
        };
        
        return logger.BeginScope(state);
    }

    /// <summary>
    /// Creates a logging scope for task execution with automatic metadata enrichment.
    /// </summary>
    /// <param name="logger">The logger to create the scope on</param>
    /// <param name="domain">The workflow domain</param>
    /// <param name="flow">The workflow flow name</param>
    /// <param name="flowVersion">The workflow flow version (e.g., "v1.0", "1")</param>
    /// <param name="instanceId">The instance ID</param>
    /// <param name="taskKey">The task key being executed</param>
    /// <param name="taskType">The task type (e.g., "HttpTask", "ScriptTask")</param>
    /// <param name="method">Auto-captured: the calling method name</param>
    /// <param name="file">Auto-captured: the source file path</param>
    /// <returns>A disposable scope object</returns>
    public static IDisposable? ForTask(
        this ILogger logger,
        string domain,
        string flow,
        string? flowVersion,
        Guid instanceId,
        string taskKey,
        string taskType,
        [CallerMemberName] string? method = null,
        [CallerFilePath] string? file = null)
    {
        var className = Path.GetFileNameWithoutExtension(file ?? "Unknown");
        
        var state = new Dictionary<string, object?>
        {
            [TelemetryConstants.ScopeFields.Domain] = domain,
            [TelemetryConstants.ScopeFields.Flow] = flow,
            [TelemetryConstants.ScopeFields.FlowVersion] = flowVersion,
            [TelemetryConstants.ScopeFields.InstanceId] = instanceId,
            [TelemetryConstants.ScopeFields.TaskKey] = taskKey,
            [TelemetryConstants.ScopeFields.TaskType] = taskType,
            [TelemetryConstants.ScopeFields.Method] = method,
            [TelemetryConstants.ScopeFields.Class] = className
        };
        
        return logger.BeginScope(state);
    }

    /// <summary>
    /// Creates a logging scope for task execution with automatic metadata enrichment including transition and trigger context.
    /// </summary>
    /// <param name="logger">The logger to create the scope on</param>
    /// <param name="domain">The workflow domain</param>
    /// <param name="flow">The workflow flow name</param>
    /// <param name="flowVersion">The workflow flow version (e.g., "v1.0", "1")</param>
    /// <param name="instanceId">The instance ID</param>
    /// <param name="transitionKey">The transition key (optional, use null if not in transition context)</param>
    /// <param name="taskKey">The task key being executed</param>
    /// <param name="taskType">The task type (e.g., "HttpTask", "ScriptTask")</param>
    /// <param name="taskTrigger">The task trigger (e.g., "Entry", "Extension", "Timer")</param>
    /// <param name="method">Auto-captured: the calling method name</param>
    /// <param name="file">Auto-captured: the source file path</param>
    /// <returns>A disposable scope object</returns>
    public static IDisposable? ForTask(
        this ILogger logger,
        string domain,
        string flow,
        string? flowVersion,
        Guid instanceId,
        string? transitionKey,
        string taskKey,
        string taskType,
        string taskTrigger,
        [CallerMemberName] string? method = null,
        [CallerFilePath] string? file = null)
    {
        var className = Path.GetFileNameWithoutExtension(file ?? "Unknown");
        
        var state = new Dictionary<string, object?>
        {
            [TelemetryConstants.ScopeFields.Domain] = domain,
            [TelemetryConstants.ScopeFields.Flow] = flow,
            [TelemetryConstants.ScopeFields.FlowVersion] = flowVersion,
            [TelemetryConstants.ScopeFields.InstanceId] = instanceId,
            [TelemetryConstants.ScopeFields.TransitionKey] = transitionKey ?? "N/A",
            [TelemetryConstants.ScopeFields.TaskKey] = taskKey,
            [TelemetryConstants.ScopeFields.TaskType] = taskType,
            [TelemetryConstants.ScopeFields.TaskTrigger] = taskTrigger,
            [TelemetryConstants.ScopeFields.Method] = method,
            [TelemetryConstants.ScopeFields.Class] = className
        };
        
        return logger.BeginScope(state);
    }

    /// <summary>
    /// Creates a logging scope for SubFlow operations with parent workflow context enrichment.
    /// </summary>
    /// <param name="logger">The logger to create the scope on</param>
    /// <param name="parentDomain">The parent workflow domain</param>
    /// <param name="parentFlow">The parent workflow flow name</param>
    /// <param name="parentFlowVersion">The parent workflow flow version</param>
    /// <param name="parentInstanceId">The parent workflow instance ID</param>
    /// <param name="transitionKey">The transition key in parent workflow</param>
    /// <param name="method">Auto-captured: the calling method name</param>
    /// <param name="file">Auto-captured: the source file path</param>
    /// <returns>A disposable scope object</returns>
    public static IDisposable? ForSubFlow(
        this ILogger logger,
        string parentDomain,
        string parentFlow,
        string? parentFlowVersion,
        Guid parentInstanceId,
        string? transitionKey = null,
        [CallerMemberName] string? method = null,
        [CallerFilePath] string? file = null)
    {
        var className = Path.GetFileNameWithoutExtension(file ?? "Unknown");
        
        var state = new Dictionary<string, object?>
        {
            [TelemetryConstants.ScopeFields.Domain] = parentDomain,
            [TelemetryConstants.ScopeFields.Flow] = parentFlow,
            [TelemetryConstants.ScopeFields.FlowVersion] = parentFlowVersion,
            [TelemetryConstants.ScopeFields.InstanceId] = parentInstanceId,
            [TelemetryConstants.ScopeFields.TransitionKey] = transitionKey ?? "N/A",
            [TelemetryConstants.ScopeFields.Method] = method,
            [TelemetryConstants.ScopeFields.Class] = className
        };
        
        return logger.BeginScope(state);
    }

    /// <summary>
    /// Creates a logging scope for background job execution.
    /// </summary>
    /// <param name="logger">The logger to create the scope on</param>
    /// <param name="jobName">The name of the job</param>
    /// <param name="jobId">The unique job identifier</param>
    /// <param name="method">Auto-captured: the calling method name</param>
    /// <param name="file">Auto-captured: the source file path</param>
    /// <returns>A disposable scope object</returns>
    public static IDisposable? ForJob(
        this ILogger logger,
        string jobName,
        string? jobId = null,
        [CallerMemberName] string? method = null,
        [CallerFilePath] string? file = null)
    {
        var className = Path.GetFileNameWithoutExtension(file ?? "Unknown");
        
        var state = new Dictionary<string, object?>
        {
            [TelemetryConstants.ScopeFields.JobName] = jobName,
            [TelemetryConstants.ScopeFields.JobId] = jobId ?? Guid.NewGuid().ToString(),
            [TelemetryConstants.ScopeFields.Method] = method,
            [TelemetryConstants.ScopeFields.Class] = className
        };
        
        return logger.BeginScope(state);
    }
}
