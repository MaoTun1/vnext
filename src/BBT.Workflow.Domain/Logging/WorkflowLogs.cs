using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Logging;

/// <summary>
/// Source-generated high-performance logging methods for workflow operations.
/// Uses LoggerMessage source generator for zero-allocation logging.
/// </summary>
public static partial class WorkflowLogs
{
    #region Transition Execution

    /// <summary>
    /// Logs when a transition starts executing.
    /// </summary>
    [LoggerMessage(
        EventId = 10001,
        Level = LogLevel.Information,
        Message = "{Prefix} Transition {TransitionKey} started for instance {InstanceId} in workflow {WorkflowKey}")]
    public static partial void TransitionStarted(
        this ILogger logger,
        string prefix,
        string transitionKey,
        Guid instanceId,
        string workflowKey);

    /// <summary>
    /// Logs when a transition completes successfully.
    /// </summary>
    [LoggerMessage(
        EventId = 10002,
        Level = LogLevel.Information,
        Message = "{Prefix} Transition {TransitionKey} completed for instance {InstanceId} in {ElapsedMs}ms")]
    public static partial void TransitionCompleted(
        this ILogger logger,
        string prefix,
        string transitionKey,
        Guid instanceId,
        long elapsedMs);

    /// <summary>
    /// Logs when a state change occurs.
    /// </summary>
    [LoggerMessage(
        EventId = 10003,
        Level = LogLevel.Information,
        Message = "{Prefix} State changed from {FromState} to {ToState} for instance {InstanceId}")]
    public static partial void StateChanged(
        this ILogger logger,
        string prefix,
        string fromState,
        string toState,
        Guid instanceId);

    /// <summary>
    /// Logs when a transition is scheduled for later execution.
    /// </summary>
    [LoggerMessage(
        EventId = 10008,
        Level = LogLevel.Information,
        Message = "{Prefix} Transition {TransitionKey} scheduled for instance {InstanceId} at {ScheduledTime}")]
    public static partial void TransitionScheduled(
        this ILogger logger,
        string prefix,
        string transitionKey,
        Guid instanceId,
        DateTimeOffset scheduledTime);

    /// <summary>
    /// Logs when a transition rule validation fails.
    /// </summary>
    [LoggerMessage(
        EventId = 10040,
        Level = LogLevel.Warning,
        Message = "{Prefix} Transition rule failed for {TransitionKey} on instance {InstanceId}: {Reason}")]
    public static partial void TransitionRuleFailed(
        this ILogger logger,
        string prefix,
        string transitionKey,
        Guid instanceId,
        string reason);

    /// <summary>
    /// Logs when automatic transition has no rule defined.
    /// </summary>
    [LoggerMessage(
        EventId = 10044,
        Level = LogLevel.Warning,
        Message = "{Prefix} Auto-transition {TransitionKey} has no rule defined")]
    public static partial void AutoTransitionNoRule(
        this ILogger logger,
        string prefix,
        string transitionKey);

    /// <summary>
    /// Logs when a transition fails with an error.
    /// </summary>
    [LoggerMessage(
        EventId = 10070,
        Level = LogLevel.Error,
        Message = "{Prefix} Transition {TransitionKey} failed for instance {InstanceId}")]
    public static partial void TransitionFailed(
        this ILogger logger,
        Exception exception,
        string prefix,
        string transitionKey,
        Guid instanceId);

    #endregion

    #region Pipeline Steps

    /// <summary>
    /// Logs when a pipeline step starts executing.
    /// </summary>
    [LoggerMessage(
        EventId = 10006,
        Level = LogLevel.Debug,
        Message = "{Prefix} Pipeline step [{StepOrder}] {StepName} started for instance {InstanceId}")]
    public static partial void PipelineStepStarted(
        this ILogger logger,
        string prefix,
        int stepOrder,
        string stepName,
        Guid instanceId);

    /// <summary>
    /// Logs when a pipeline step completes.
    /// </summary>
    [LoggerMessage(
        EventId = 10007,
        Level = LogLevel.Debug,
        Message = "{Prefix} Pipeline step [{StepOrder}] {StepName} completed for instance {InstanceId} in {ElapsedMs}ms")]
    public static partial void PipelineStepCompleted(
        this ILogger logger,
        string prefix,
        int stepOrder,
        string stepName,
        Guid instanceId,
        long elapsedMs);

    /// <summary>
    /// Logs when a pipeline step fails.
    /// </summary>
    [LoggerMessage(
        EventId = 10072,
        Level = LogLevel.Error,
        Message = "{Prefix} Pipeline step [{StepOrder}] {StepName} failed for instance {InstanceId}")]
    public static partial void PipelineStepFailed(
        this ILogger logger,
        Exception exception,
        string prefix,
        int stepOrder,
        string stepName,
        Guid instanceId);

    #endregion

    #region Strategy Execution

    /// <summary>
    /// Logs when a transition execution strategy starts.
    /// </summary>
    [LoggerMessage(
        EventId = 10200,
        Level = LogLevel.Debug,
        Message = "{Prefix} Strategy {StrategyName} started for transition {TransitionKey} on instance {InstanceId}")]
    public static partial void StrategyExecutionStarted(
        this ILogger logger,
        string prefix,
        string strategyName,
        string transitionKey,
        Guid instanceId);

    /// <summary>
    /// Logs when a transition execution strategy completes successfully.
    /// </summary>
    [LoggerMessage(
        EventId = 10201,
        Level = LogLevel.Debug,
        Message = "{Prefix} Strategy {StrategyName} completed for transition {TransitionKey} in {ElapsedMs}ms")]
    public static partial void StrategyExecutionCompleted(
        this ILogger logger,
        string prefix,
        string strategyName,
        string transitionKey,
        long elapsedMs);

    /// <summary>
    /// Logs when context is created by the strategy.
    /// </summary>
    [LoggerMessage(
        EventId = 10202,
        Level = LogLevel.Debug,
        Message = "{Prefix} TransitionExecutionContext created for {TransitionKey} with handler {HandlerType}")]
    public static partial void ContextCreated(
        this ILogger logger,
        string prefix,
        string transitionKey,
        string handlerType);

    #endregion

    #region Handler Execution

    /// <summary>
    /// Logs when a transition handler's pre-handling starts.
    /// </summary>
    [LoggerMessage(
        EventId = 10250,
        Level = LogLevel.Debug,
        Message = "{Prefix} Handler {HandlerName} pre-handling started for {TriggerType} transition {TransitionKey}")]
    public static partial void HandlerPreHandleStarted(
        this ILogger logger,
        string prefix,
        string handlerName,
        string triggerType,
        string transitionKey);

    /// <summary>
    /// Logs when a transition handler's pre-handling completes.
    /// </summary>
    [LoggerMessage(
        EventId = 10251,
        Level = LogLevel.Debug,
        Message = "{Prefix} Handler {HandlerName} pre-handling completed for {TriggerType} transition in {ElapsedMs}ms")]
    public static partial void HandlerPreHandleCompleted(
        this ILogger logger,
        string prefix,
        string handlerName,
        string triggerType,
        long elapsedMs);

    /// <summary>
    /// Logs when a transition handler's post-handling starts.
    /// </summary>
    [LoggerMessage(
        EventId = 10252,
        Level = LogLevel.Debug,
        Message = "{Prefix} Handler {HandlerName} post-handling started for {TriggerType} transition {TransitionKey}")]
    public static partial void HandlerPostHandleStarted(
        this ILogger logger,
        string prefix,
        string handlerName,
        string triggerType,
        string transitionKey);

    /// <summary>
    /// Logs when a transition handler's post-handling completes.
    /// </summary>
    [LoggerMessage(
        EventId = 10253,
        Level = LogLevel.Debug,
        Message = "{Prefix} Handler {HandlerName} post-handling completed for {TriggerType} transition in {ElapsedMs}ms")]
    public static partial void HandlerPostHandleCompleted(
        this ILogger logger,
        string prefix,
        string handlerName,
        string triggerType,
        long elapsedMs);

    /// <summary>
    /// Logs when a transition handler pre-handling fails.
    /// </summary>
    [LoggerMessage(
        EventId = 10280,
        Level = LogLevel.Error,
        Message = "{Prefix} Handler {HandlerName} pre-handling failed for {TriggerType} transition {TransitionKey}")]
    public static partial void HandlerPreHandleFailed(
        this ILogger logger,
        Exception exception,
        string prefix,
        string handlerName,
        string triggerType,
        string transitionKey);

    /// <summary>
    /// Logs when a transition handler post-handling fails.
    /// </summary>
    [LoggerMessage(
        EventId = 10281,
        Level = LogLevel.Error,
        Message = "{Prefix} Handler {HandlerName} post-handling failed for {TriggerType} transition {TransitionKey}")]
    public static partial void HandlerPostHandleFailed(
        this ILogger logger,
        Exception exception,
        string prefix,
        string handlerName,
        string triggerType,
        string transitionKey);

    #endregion

    #region Task Execution

    /// <summary>
    /// Logs when a task starts executing.
    /// </summary>
    [LoggerMessage(
        EventId = 10004,
        Level = LogLevel.Information,
        Message = "{Prefix} Task {TaskKey} ({TaskType}) started for instance {InstanceId}")]
    public static partial void TaskExecutionStarted(
        this ILogger logger,
        string prefix,
        string taskKey,
        string taskType,
        Guid instanceId);

    /// <summary>
    /// Logs when a task completes successfully.
    /// </summary>
    [LoggerMessage(
        EventId = 10005,
        Level = LogLevel.Information,
        Message = "{Prefix} Task {TaskKey} ({TaskType}) completed for instance {InstanceId} in {ElapsedMs}ms")]
    public static partial void TaskExecutionCompleted(
        this ILogger logger,
        string prefix,
        string taskKey,
        string taskType,
        Guid instanceId,
        long elapsedMs);

    /// <summary>
    /// Logs when a task execution encounters a warning.
    /// </summary>
    [LoggerMessage(
        EventId = 10041,
        Level = LogLevel.Warning,
        Message = "{Prefix} Task {TaskKey} warning for instance {InstanceId}: {Message}")]
    public static partial void TaskExecutionWarning(
        this ILogger logger,
        string prefix,
        string taskKey,
        Guid instanceId,
        string message);

    /// <summary>
    /// Logs when a task execution fails.
    /// </summary>
    [LoggerMessage(
        EventId = 10071,
        Level = LogLevel.Error,
        Message = "{Prefix} Task {TaskKey} ({TaskType}) failed for instance {InstanceId}")]
    public static partial void TaskExecutionFailed(
        this ILogger logger,
        Exception exception,
        string prefix,
        string taskKey,
        string taskType,
        Guid instanceId);

    #endregion

    #region SubFlow

    /// <summary>
    /// Logs when a subflow starts.
    /// </summary>
    [LoggerMessage(
        EventId = 10009,
        Level = LogLevel.Information,
        Message = "{Prefix} SubFlow {SubFlowKey} started with instance {SubFlowInstanceId} from parent {ParentInstanceId}")]
    public static partial void SubFlowStarted(
        this ILogger logger,
        string prefix,
        string subFlowKey,
        Guid subFlowInstanceId,
        Guid parentInstanceId);

    /// <summary>
    /// Logs when a subflow completes.
    /// </summary>
    [LoggerMessage(
        EventId = 10010,
        Level = LogLevel.Information,
        Message = "{Prefix} SubFlow {SubFlowKey} completed for instance {SubFlowInstanceId} in {ElapsedMs}ms")]
    public static partial void SubFlowCompleted(
        this ILogger logger,
        string prefix,
        string subFlowKey,
        Guid subFlowInstanceId,
        long elapsedMs);

    /// <summary>
    /// Logs when SubFlow configuration is missing or invalid.
    /// </summary>
    [LoggerMessage(
        EventId = 10074,
        Level = LogLevel.Error,
        Message = "{Prefix} SubFlow configuration invalid for state {StateName} on instance {InstanceId}")]
    public static partial void SubFlowConfigInvalid(
        this ILogger logger,
        string prefix,
        string stateName,
        Guid instanceId);

    /// <summary>
    /// Logs when a SubFlow completion event is received.
    /// </summary>
    [LoggerMessage(
        EventId = 40011,
        Level = LogLevel.Information,
        Message = "{Prefix} SubFlow completion event received for SubInstance {SubInstanceId}, Parent {ParentInstanceId} in {Domain}/{Flow}")]
    public static partial void SubFlowEventReceived(
        this ILogger logger,
        string prefix,
        Guid subInstanceId,
        Guid parentInstanceId,
        string domain,
        string flow);

    /// <summary>
    /// Logs when a correlation is not found for a completed SubFlow.
    /// </summary>
    [LoggerMessage(
        EventId = 40043,
        Level = LogLevel.Warning,
        Message = "{Prefix} Correlation not found for SubFlow instance {SubInstanceId}")]
    public static partial void SubFlowCorrelationNotFound(
        this ILogger logger,
        string prefix,
        Guid subInstanceId);

    /// <summary>
    /// Logs when a correlation is marked as completed.
    /// </summary>
    [LoggerMessage(
        EventId = 40012,
        Level = LogLevel.Information,
        Message = "{Prefix} SubFlow correlation completed for SubInstance {SubInstanceId}, Parent {ParentInstanceId}")]
    public static partial void SubFlowCorrelationCompleted(
        this ILogger logger,
        string prefix,
        Guid subInstanceId,
        Guid parentInstanceId);

    /// <summary>
    /// Logs when parent workflow continuation starts after SubFlow completion.
    /// </summary>
    [LoggerMessage(
        EventId = 40013,
        Level = LogLevel.Information,
        Message = "{Prefix} Parent workflow continuation started for instance {ParentInstanceId} in state {CurrentState}")]
    public static partial void SubFlowParentContinuationStarted(
        this ILogger logger,
        string prefix,
        Guid parentInstanceId,
        string currentState);

    /// <summary>
    /// Logs when SubFlow output mapping starts.
    /// </summary>
    [LoggerMessage(
        EventId = 40014,
        Level = LogLevel.Information,
        Message = "{Prefix} SubFlow output mapping started for parent instance {ParentInstanceId}")]
    public static partial void SubFlowOutputMappingStarted(
        this ILogger logger,
        string prefix,
        Guid parentInstanceId);

    /// <summary>
    /// Logs when pipeline is resumed after SubFlow completion.
    /// </summary>
    [LoggerMessage(
        EventId = 40015,
        Level = LogLevel.Information,
        Message = "{Prefix} Resuming pipeline for parent instance {ParentInstanceId} after SubFlow completion")]
    public static partial void SubFlowPipelineResumed(
        this ILogger logger,
        string prefix,
        Guid parentInstanceId);

    /// <summary>
    /// Logs when SubFlow completion processing fails.
    /// </summary>
    [LoggerMessage(
        EventId = 40073,
        Level = LogLevel.Error,
        Message = "{Prefix} SubFlow completion failed for SubInstance {SubInstanceId}, Parent {ParentInstanceId}")]
    public static partial void SubFlowCompletionFailed(
        this ILogger logger,
        Exception exception,
        string prefix,
        Guid subInstanceId,
        Guid parentInstanceId);

    #endregion

    #region Instance Management

    /// <summary>
    /// Logs when a workflow instance is created.
    /// </summary>
    [LoggerMessage(
        EventId = 20001,
        Level = LogLevel.Information,
        Message = "{Prefix} Instance {InstanceId} created for workflow {WorkflowKey} with initial state {InitialState}")]
    public static partial void InstanceCreated(
        this ILogger logger,
        string prefix,
        Guid instanceId,
        string workflowKey,
        string initialState);

    /// <summary>
    /// Logs when a workflow instance is updated.
    /// </summary>
    [LoggerMessage(
        EventId = 20002,
        Level = LogLevel.Debug,
        Message = "{Prefix} Instance {InstanceId} updated")]
    public static partial void InstanceUpdated(
        this ILogger logger,
        string prefix,
        Guid instanceId);

    /// <summary>
    /// Logs when an instance is not found.
    /// </summary>
    [LoggerMessage(
        EventId = 20040,
        Level = LogLevel.Warning,
        Message = "{Prefix} Instance {InstanceId} not found for workflow {WorkflowKey}")]
    public static partial void InstanceNotFound(
        this ILogger logger,
        string prefix,
        Guid instanceId,
        string workflowKey);

    /// <summary>
    /// Logs when instance creation fails.
    /// </summary>
    [LoggerMessage(
        EventId = 20070,
        Level = LogLevel.Error,
        Message = "{Prefix} Failed to create instance for workflow {WorkflowKey}")]
    public static partial void InstanceCreationFailed(
        this ILogger logger,
        Exception exception,
        string prefix,
        string workflowKey);

    #endregion

    #region Background Jobs

    /// <summary>
    /// Logs when a background job starts execution.
    /// </summary>
    [LoggerMessage(
        EventId = 20005,
        Level = LogLevel.Information,
        Message = "{Prefix} Job {JobName} started with ID {JobId}")]
    public static partial void JobExecutionStarted(
        this ILogger logger,
        string prefix,
        string jobName,
        string jobId);

    /// <summary>
    /// Logs when a background job completes.
    /// </summary>
    [LoggerMessage(
        EventId = 20006,
        Level = LogLevel.Information,
        Message = "{Prefix} Job {JobName} completed in {ElapsedMs}ms")]
    public static partial void JobExecutionCompleted(
        this ILogger logger,
        string prefix,
        string jobName,
        long elapsedMs);

    /// <summary>
    /// Logs when a background job is retrying.
    /// </summary>
    [LoggerMessage(
        EventId = 20042,
        Level = LogLevel.Warning,
        Message = "{Prefix} Job {JobName} retrying (attempt {Attempt})")]
    public static partial void JobRetrying(
        this ILogger logger,
        string prefix,
        string jobName,
        int attempt);

    /// <summary>
    /// Logs when a background job fails.
    /// </summary>
    [LoggerMessage(
        EventId = 20072,
        Level = LogLevel.Error,
        Message = "{Prefix} Job {JobName} failed")]
    public static partial void JobExecutionFailed(
        this ILogger logger,
        Exception exception,
        string prefix,
        string jobName);

    #endregion
}

