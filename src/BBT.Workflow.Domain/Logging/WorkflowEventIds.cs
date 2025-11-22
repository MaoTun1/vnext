using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Logging;

/// <summary>
/// Centralized EventId definitions for workflow logging.
/// EventIds follow a structured numbering scheme:
/// - 10xxx: Execution layer
/// - 20xxx: Orchestration layer  
/// - 30xxx: Infrastructure layer
/// - 40xxx: Application layer
/// - 50xxx: Domain layer
/// 
/// Within each range:
/// - xx01-xx39: Information level
/// - xx40-xx69: Warning level
/// - xx70-xx99: Error level
/// </summary>
public static class WorkflowEventIds
{
    #region Execution Layer (10xxx)

    // Information (10001-10039)
    public static readonly EventId TransitionStarted = new(10001, nameof(TransitionStarted));
    public static readonly EventId TransitionCompleted = new(10002, nameof(TransitionCompleted));
    public static readonly EventId StateChanged = new(10003, nameof(StateChanged));
    public static readonly EventId TaskExecutionStarted = new(10004, nameof(TaskExecutionStarted));
    public static readonly EventId TaskExecutionCompleted = new(10005, nameof(TaskExecutionCompleted));
    public static readonly EventId PipelineStepStarted = new(10006, nameof(PipelineStepStarted));
    public static readonly EventId PipelineStepCompleted = new(10007, nameof(PipelineStepCompleted));
    public static readonly EventId TransitionScheduled = new(10008, nameof(TransitionScheduled));
    public static readonly EventId SubFlowStarted = new(10009, nameof(SubFlowStarted));
    public static readonly EventId SubFlowCompleted = new(10010, nameof(SubFlowCompleted));
    
    // Strategy Execution (10200-10239)
    public static readonly EventId StrategyExecutionStarted = new(10200, nameof(StrategyExecutionStarted));
    public static readonly EventId StrategyExecutionCompleted = new(10201, nameof(StrategyExecutionCompleted));
    public static readonly EventId ContextCreated = new(10202, nameof(ContextCreated));
    
    // Handler Execution (10250-10289)
    public static readonly EventId HandlerPreHandleStarted = new(10250, nameof(HandlerPreHandleStarted));
    public static readonly EventId HandlerPreHandleCompleted = new(10251, nameof(HandlerPreHandleCompleted));
    public static readonly EventId HandlerPostHandleStarted = new(10252, nameof(HandlerPostHandleStarted));
    public static readonly EventId HandlerPostHandleCompleted = new(10253, nameof(HandlerPostHandleCompleted));

    // Warning (10040-10069)
    public static readonly EventId TransitionRuleFailed = new(10040, nameof(TransitionRuleFailed));
    public static readonly EventId TaskExecutionWarning = new(10041, nameof(TaskExecutionWarning));
    public static readonly EventId TransitionRetrying = new(10042, nameof(TransitionRetrying));
    public static readonly EventId StateValidationWarning = new(10043, nameof(StateValidationWarning));
    public static readonly EventId AutoTransitionNoRule = new(10044, nameof(AutoTransitionNoRule));

    // Error (10070-10099)
    public static readonly EventId TransitionFailed = new(10070, nameof(TransitionFailed));
    public static readonly EventId TaskExecutionFailed = new(10071, nameof(TaskExecutionFailed));
    public static readonly EventId PipelineStepFailed = new(10072, nameof(PipelineStepFailed));
    public static readonly EventId StateTransitionError = new(10073, nameof(StateTransitionError));
    public static readonly EventId SubFlowConfigInvalid = new(10074, nameof(SubFlowConfigInvalid));
    public static readonly EventId HandlerPreHandleFailed = new(10280, nameof(HandlerPreHandleFailed));
    public static readonly EventId HandlerPostHandleFailed = new(10281, nameof(HandlerPostHandleFailed));

    #endregion

    #region Orchestration Layer (20xxx)

    // Information (20001-20039)
    public static readonly EventId InstanceCreated = new(20001, nameof(InstanceCreated));
    public static readonly EventId InstanceUpdated = new(20002, nameof(InstanceUpdated));
    public static readonly EventId WorkflowDefinitionLoaded = new(20003, nameof(WorkflowDefinitionLoaded));
    public static readonly EventId JobScheduled = new(20004, nameof(JobScheduled));
    public static readonly EventId JobExecutionStarted = new(20005, nameof(JobExecutionStarted));
    public static readonly EventId JobExecutionCompleted = new(20006, nameof(JobExecutionCompleted));
    public static readonly EventId EventReceived = new(20007, nameof(EventReceived));
    public static readonly EventId EventPublished = new(20008, nameof(EventPublished));

    // Warning (20040-20069)
    public static readonly EventId InstanceNotFound = new(20040, nameof(InstanceNotFound));
    public static readonly EventId WorkflowValidationWarning = new(20041, nameof(WorkflowValidationWarning));
    public static readonly EventId JobRetrying = new(20042, nameof(JobRetrying));
    public static readonly EventId EventProcessingWarning = new(20043, nameof(EventProcessingWarning));

    // Error (20070-20099)
    public static readonly EventId InstanceCreationFailed = new(20070, nameof(InstanceCreationFailed));
    public static readonly EventId WorkflowDefinitionError = new(20071, nameof(WorkflowDefinitionError));
    public static readonly EventId JobExecutionFailed = new(20072, nameof(JobExecutionFailed));
    public static readonly EventId EventPublishingFailed = new(20073, nameof(EventPublishingFailed));

    #endregion

    #region Application Layer (40xxx)

    // Information (40001-40039)
    public static readonly EventId ServiceOperationStarted = new(40001, nameof(ServiceOperationStarted));
    public static readonly EventId ServiceOperationCompleted = new(40002, nameof(ServiceOperationCompleted));
    public static readonly EventId ValidationSucceeded = new(40003, nameof(ValidationSucceeded));
    public static readonly EventId MappingPerformed = new(40004, nameof(MappingPerformed));
    public static readonly EventId ScriptCompiled = new(40005, nameof(ScriptCompiled));
    public static readonly EventId ScriptExecuted = new(40006, nameof(ScriptExecuted));

    // Warning (40040-40069)
    public static readonly EventId ValidationWarning = new(40040, nameof(ValidationWarning));
    public static readonly EventId ServiceOperationWarning = new(40041, nameof(ServiceOperationWarning));
    public static readonly EventId ScriptCompilationWarning = new(40042, nameof(ScriptCompilationWarning));

    // Error (40070-40099)
    public static readonly EventId ServiceOperationFailed = new(40070, nameof(ServiceOperationFailed));
    public static readonly EventId ValidationFailed = new(40071, nameof(ValidationFailed));
    public static readonly EventId ScriptExecutionFailed = new(40072, nameof(ScriptExecutionFailed));

    #endregion
}

