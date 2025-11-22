namespace BBT.Workflow.Logging;

/// <summary>
/// Constants for telemetry prefixes and metadata used throughout the vNext workflow system.
/// Centralizes all telemetry-related string constants for maintainability.
/// </summary>
public static class TelemetryConstants
{
    /// <summary>
    /// OpenTelemetry ActivitySource name for workflow operations.
    /// Used for distributed tracing of workflow execution.
    /// </summary>
    public const string ActivitySourceName = "BBT.Workflow";
    
    /// <summary>
    /// OpenTelemetry ActivitySource version.
    /// </summary>
    public const string ActivitySourceVersion = "1.0.0";

    /// <summary>
    /// Telemetry prefix constants for different layers of the application.
    /// Used in structured logging and OpenTelemetry spans to identify the source layer.
    /// </summary>
    public static class Prefixes
    {
        /// <summary>
        /// Execution layer prefix - used for workflow execution operations, transitions, and pipeline steps.
        /// </summary>
        public const string Execution = "vnext.exec";

        /// <summary>
        /// Application layer prefix - used for application services and business logic.
        /// </summary>
        public const string Application = "vnext.app";

        /// <summary>
        /// Orchestration layer prefix - used for orchestration API and coordination logic.
        /// </summary>
        public const string Orchestration = "vnext.orch";

        /// <summary>
        /// Infrastructure layer prefix - used for database, caching, HTTP, and external system operations.
        /// </summary>
        public const string Infrastructure = "vnext.infra";

        /// <summary>
        /// Domain layer prefix - used for domain events and business rules.
        /// </summary>
        public const string Domain = "vnext.domain";
    }

    /// <summary>
    /// Scope metadata field names used in structured logging.
    /// </summary>
    public static class ScopeFields
    {
        /// <summary>
        /// Workflow domain field name.
        /// </summary>
        public const string Domain = "domain";

        /// <summary>
        /// Workflow flow (definition key) field name.
        /// </summary>
        public const string Flow = "flow";

        /// <summary>
        /// Workflow flow version field name.
        /// </summary>
        public const string FlowVersion = "flowVersion";

        /// <summary>
        /// Instance ID field name.
        /// </summary>
        public const string InstanceId = "instanceId";

        /// <summary>
        /// Transition key field name.
        /// </summary>
        public const string TransitionKey = "transitionKey";

        /// <summary>
        /// Task key field name.
        /// </summary>
        public const string TaskKey = "taskKey";

        /// <summary>
        /// Task type field name.
        /// </summary>
        public const string TaskType = "taskType";

        /// <summary>
        /// Task trigger field name.
        /// </summary>
        public const string TaskTrigger = "taskTrigger";

        /// <summary>
        /// Step order field name.
        /// </summary>
        public const string StepOrder = "stepOrder";

        /// <summary>
        /// Method name field name.
        /// </summary>
        public const string Method = "method";

        /// <summary>
        /// Class name field name.
        /// </summary>
        public const string Class = "class";

        /// <summary>
        /// Job name field name.
        /// </summary>
        public const string JobName = "jobName";

        /// <summary>
        /// Job ID field name.
        /// </summary>
        public const string JobId = "jobId";
    }
    
    /// <summary>
    /// Span and activity names for distributed tracing.
    /// </summary>
    public static class SpanNames
    {
        /// <summary>
        /// Transition execution span name.
        /// </summary>
        public const string TransitionExecution = "transition.execute";
        
        /// <summary>
        /// Handler pre-handle span name.
        /// </summary>
        public const string HandlerPreHandle = "handler.prehandle";
        
        /// <summary>
        /// Handler post-handle span name.
        /// </summary>
        public const string HandlerPostHandle = "handler.posthandle";
        
        /// <summary>
        /// Pipeline execution span name.
        /// </summary>
        public const string PipelineExecution = "pipeline.execute";
        
        /// <summary>
        /// Pipeline step span name.
        /// </summary>
        public const string PipelineStep = "pipeline.step";
        
        /// <summary>
        /// Task execution span name.
        /// </summary>
        public const string TaskExecution = "task.execute";
        
        /// <summary>
        /// SubFlow start span name.
        /// </summary>
        public const string SubFlowStart = "subflow.start";
        
        /// <summary>
        /// SubFlow complete span name.
        /// </summary>
        public const string SubFlowComplete = "subflow.complete";
    }
    
    /// <summary>
    /// Tag names for span attributes.
    /// </summary>
    public static class TagNames
    {
        public const string Domain = "vnext.domain";
        public const string Flow = "vnext.flow.key";
        public const string FlowVersion = "vnext.flow.version";
        public const string InstanceId = "vnext.instance.id";
        public const string TransitionKey = "vnext.transition.key";
        public const string TriggerType = "vnext.trigger.type";
        public const string HandlerName = "vnext.handler.name";
        public const string StepName = "vnext.step.name";
        public const string StepOrder = "vnext.step.order";
        public const string TaskKey = "vnext.task.key";
        public const string TaskType = "vnext.task.type";
        public const string SubFlowKey = "vnext.subflow.key";
        public const string StateFrom = "vnext.state.from";
        public const string StateTo = "vnext.state.to";
    }
}

