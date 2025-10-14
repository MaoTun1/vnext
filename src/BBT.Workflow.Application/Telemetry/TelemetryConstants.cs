namespace BBT.Workflow.Telemetry;

/// <summary>
/// Constants for telemetry prefixes and metadata used throughout the vNext workflow system.
/// Centralizes all telemetry-related string constants for maintainability.
/// </summary>
public static class TelemetryConstants
{
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
}

