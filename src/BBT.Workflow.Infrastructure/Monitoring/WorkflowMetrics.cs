using Prometheus;

namespace BBT.Workflow.Monitoring;

/// <summary>
/// Provides Prometheus metrics for workflow monitoring and observability.
/// Contains metrics for instances, tasks, database operations, caching, and error tracking.
/// Located in Infrastructure layer as per DDD principles for cross-cutting concerns.
/// </summary>
public static class WorkflowMetrics
{
    // Instance metrics
    /// <summary>
    /// Total workflow instances created counter with workflow and domain labels.
    /// </summary>
    public static readonly Counter InstancesCreated = Metrics
        .CreateCounter("workflow_instances_created_total",
            "Total workflow instances created",
            new[] { "workflow", "domain" });

    /// <summary>
    /// Total workflow instances completed counter with workflow and domain labels.
    /// </summary>
    public static readonly Counter InstancesCompleted = Metrics
        .CreateCounter("workflow_instances_completed_total",
            "Total workflow instances completed",
            new[] { "workflow", "domain" });

    /// <summary>
    /// Total workflow instances timed out counter with workflow and domain labels.
    /// </summary>
    public static readonly Counter InstancesTimedOut = Metrics
        .CreateCounter("workflow_instances_timeout_total",
            "Total workflow instances that exceeded their timeout duration",
            new[] { "workflow", "domain" });

    /// <summary>
    /// Workflow instance execution duration histogram with workflow and status labels.
    /// Uses exponential buckets for better performance measurement.
    /// </summary>
    public static readonly Histogram InstanceDuration = Metrics
        .CreateHistogram("workflow_instance_duration_seconds",
            "Workflow instance execution duration",
            new[] { "workflow", "status" },
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.1, 2, 10)
            });

    /// <summary>
    /// Currently active workflow instances gauge with workflow label.
    /// </summary>
    public static readonly Gauge ActiveInstances = Metrics
        .CreateGauge("workflow_instances_active",
            "Currently active workflow instances",
            new[] { "workflow" });

    /// <summary>
    /// Currently suspended workflow instances gauge with workflow label.
    /// </summary>
    public static readonly Gauge SuspendedInstances = Metrics
        .CreateGauge("workflow_instances_suspended",
            "Currently suspended workflow instances",
            new[] { "workflow" });

    /// <summary>
    /// Currently pending (busy) workflow instances gauge with workflow label.
    /// </summary>
    public static readonly Gauge PendingInstances = Metrics
        .CreateGauge("workflow_instances_pending",
            "Currently pending (busy) workflow instances",
            new[] { "workflow" });

    // Task metrics
    /// <summary>
    /// Total tasks executed counter with task_type and workflow labels.
    /// </summary>
    public static readonly Counter TasksExecuted = Metrics
        .CreateCounter("workflow_tasks_executed_total",
            "Total tasks executed",
            new[] { "task_type", "workflow" });

    /// <summary>
    /// Total tasks completed successfully counter with task_type and workflow labels.
    /// </summary>
    public static readonly Counter TasksCompleted = Metrics
        .CreateCounter("workflow_tasks_completed_total",
            "Successfully completed tasks",
            new[] { "task_type", "workflow" });

    /// <summary>
    /// Total tasks failed counter with task_type and workflow labels.
    /// </summary>
    public static readonly Counter TasksFailed = Metrics
        .CreateCounter("workflow_tasks_failed_total",
            "Failed tasks",
            new[] { "task_type", "workflow" });

    /// <summary>
    /// Total task retry attempts counter with task_type and workflow labels.
    /// </summary>
    public static readonly Counter TasksRetried = Metrics
        .CreateCounter("workflow_tasks_retried_total",
            "Task retry attempts",
            new[] { "task_type", "workflow" });

    /// <summary>
    /// Task execution duration histogram with task_type and workflow labels.
    /// Uses custom buckets for task execution timing.
    /// </summary>
    public static readonly Histogram TaskDuration = Metrics
        .CreateHistogram("workflow_task_duration_seconds",
            "Task execution duration",
            new[] { "task_type", "workflow" },
            new HistogramConfiguration
            {
                Buckets = new[] { 0.1, 0.5, 1, 2, 5, 10, 30, 60 }
            });

    /// <summary>
    /// Task queue wait time histogram with task_type label.
    /// Measures time tasks spend waiting in queue before execution.
    /// </summary>
    public static readonly Histogram TaskQueueWaitDuration = Metrics
        .CreateHistogram("workflow_task_queue_wait_seconds",
            "Time tasks wait in queue",
            new[] { "task_type" },
            new HistogramConfiguration
            {
                Buckets = new[] { 0.01, 0.05, 0.1, 0.5, 1, 2, 5, 10, 30 }
            });

    // Task execution state metrics
    /// <summary>
    /// Tasks waiting to be executed gauge with task_type and workflow labels.
    /// </summary>
    public static readonly Gauge TasksPending = Metrics
        .CreateGauge("workflow_tasks_pending",
            "Tasks waiting to be executed",
            new[] { "task_type", "workflow" });

    /// <summary>
    /// Currently executing tasks gauge with task_type and workflow labels.
    /// </summary>
    public static readonly Gauge TasksRunning = Metrics
        .CreateGauge("workflow_tasks_running",
            "Currently executing tasks",
            new[] { "task_type", "workflow" });

    // Task pool metrics
    /// <summary>
    /// Current task factory pool size gauge with task_type label.
    /// </summary>
    public static readonly Gauge TaskPoolSize = Metrics
        .CreateGauge("task_factory_pool_size",
            "Current task factory pool size",
            new[] { "task_type" });

    /// <summary>
    /// Available objects in pool gauge with task_type label.
    /// </summary>
    public static readonly Gauge TaskFactoryPoolAvailable = Metrics
        .CreateGauge("task_factory_pool_available",
            "Available objects in task factory pool",
            new[] { "task_type" });

    /// <summary>
    /// Objects currently in use gauge with task_type label.
    /// </summary>
    public static readonly Gauge TaskFactoryPoolInUse = Metrics
        .CreateGauge("task_factory_pool_in_use",
            "Objects currently in use from task factory pool",
            new[] { "task_type" });

    /// <summary>
    /// Total object rentals counter with task_type label.
    /// </summary>
    public static readonly Counter TaskFactoryPoolRentals = Metrics
        .CreateCounter("task_factory_pool_rentals_total",
            "Total object rentals from task factory pool",
            new[] { "task_type" });

    /// <summary>
    /// Total object returns counter with task_type label.
    /// </summary>
    public static readonly Counter TaskFactoryPoolReturns = Metrics
        .CreateCounter("task_factory_pool_returns_total",
            "Total object returns to task factory pool",
            new[] { "task_type" });

    /// <summary>
    /// New objects created counter with task_type label.
    /// </summary>
    public static readonly Counter TaskFactoryPoolCreates = Metrics
        .CreateCounter("task_factory_pool_creates_total",
            "New objects created for task factory pool",
            new[] { "task_type" });

    // Database metrics
    /// <summary>
    /// Database query execution time histogram with query_type and table labels.
    /// </summary>
    public static readonly Histogram DbQueryDuration = Metrics
        .CreateHistogram("workflow_db_query_duration_seconds",
            "Database query execution time",
            new[] { "query_type", "table" });

    /// <summary>
    /// Database transaction duration histogram with operation label.
    /// </summary>
    public static readonly Histogram DbTransactionDuration = Metrics
        .CreateHistogram("workflow_db_transaction_duration_seconds",
            "Database transaction duration",
            new[] { "operation" },
            new HistogramConfiguration
            {
                Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10 }
            });

    /// <summary>
    /// Total database queries counter with query_type, table, and status labels.
    /// </summary>
    public static readonly Counter DbQueries = Metrics
        .CreateCounter("workflow_db_queries_total",
            "Total database queries",
            new[] { "query_type", "table", "status" });

    /// <summary>
    /// Database operation errors counter with operation, table, and error_type labels.
    /// </summary>
    public static readonly Counter DbErrors = Metrics
        .CreateCounter("workflow_db_errors_total",
            "Database operation errors",
            new[] { "operation", "table", "error_type" });

    /// <summary>
    /// Database connection attempts counter with connection_type and status labels.
    /// </summary>
    public static readonly Counter DbConnections = Metrics
        .CreateCounter("workflow_db_connections_total",
            "Database connection attempts",
            new[] { "connection_type", "status" });

    // Cache metrics
    /// <summary>
    /// Cache hit count counter with cache_name label.
    /// </summary>
    public static readonly Counter CacheHits = Metrics
        .CreateCounter("workflow_cache_hits_total",
            "Cache hit count",
            new[] { "cache_name" });

    /// <summary>
    /// Cache miss count counter with cache_name label.
    /// </summary>
    public static readonly Counter CacheMisses = Metrics
        .CreateCounter("workflow_cache_misses_total",
            "Cache miss count",
            new[] { "cache_name" });

    /// <summary>
    /// Cache evictions counter with cache_name and reason labels.
    /// </summary>
    public static readonly Counter CacheEvictions = Metrics
        .CreateCounter("workflow_cache_evictions_total",
            "Cache evictions count",
            new[] { "cache_name", "reason" });

    /// <summary>
    /// Current cache size in bytes gauge with cache_name label.
    /// </summary>
    public static readonly Gauge CacheSizeBytes = Metrics
        .CreateGauge("workflow_cache_size_bytes",
            "Current cache size in bytes",
            new[] { "cache_name" });

    /// <summary>
    /// Number of cached entries gauge with cache_name label.
    /// </summary>
    public static readonly Gauge CacheEntries = Metrics
        .CreateGauge("workflow_cache_entries",
            "Number of cached entries",
            new[] { "cache_name" });

    // HTTP metrics
    /// <summary>
    /// HTTP requests counter with method, endpoint, and status_code labels.
    /// </summary>
    public static readonly Counter HttpRequests = Metrics
        .CreateCounter("http_requests_total",
            "HTTP requests",
            new[] { "method", "endpoint", "status_code" });

    /// <summary>
    /// HTTP request errors counter with method, endpoint, and error_type labels.
    /// </summary>
    public static readonly Counter HttpRequestErrors = Metrics
        .CreateCounter("http_request_errors_total",
            "HTTP request errors",
            new[] { "method", "endpoint", "error_type" });

    /// <summary>
    /// HTTP request duration histogram with method, endpoint, and status_code labels.
    /// </summary>
    public static readonly Histogram HttpRequestDuration = Metrics
        .CreateHistogram("http_request_duration_seconds",
            "HTTP request processing time",
            new[] { "method", "endpoint", "status_code" },
            new HistogramConfiguration
            {
                Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10 }
            });

    /// <summary>
    /// HTTP response size histogram with method, endpoint, and status_code labels.
    /// </summary>
    public static readonly Histogram HttpResponseSize = Metrics
        .CreateHistogram("http_response_size_bytes",
            "HTTP response payload size",
            new[] { "method", "endpoint", "status_code" },
            new HistogramConfiguration
            {
                Buckets = new[] { 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, 10000000.0 }
            });

    // Background job metrics
    /// <summary>
    /// Jobs executed counter with job_type and status labels.
    /// </summary>
    public static readonly Counter JobsExecuted = Metrics
        .CreateCounter("background_jobs_executed_total",
            "Jobs executed",
            new[] { "job_type", "status" });

    /// <summary>
    /// Pending jobs in queue gauge with job_type label.
    /// </summary>
    public static readonly Gauge JobsPending = Metrics
        .CreateGauge("background_jobs_pending",
            "Pending jobs in queue",
            new[] { "job_type" });

    // Error metrics
    /// <summary>
    /// Total errors counter with error_type, severity, and component labels.
    /// </summary>
    public static readonly Counter Errors = Metrics
        .CreateCounter("workflow_errors_total",
            "Total errors",
            new[] { "error_type", "severity", "component" });

    // State transition metrics
    /// <summary>
    /// Total state transitions counter with workflow, from_state, and to_state labels.
    /// </summary>
    public static readonly Counter StateTransitions = Metrics
        .CreateCounter("workflow_state_transitions_total",
            "Total state transitions",
            new[] { "workflow", "from_state", "to_state" });

    /// <summary>
    /// State entry count counter with workflow and state labels.
    /// </summary>
    public static readonly Counter StateEntries = Metrics
        .CreateCounter("workflow_state_entries_total",
            "State entry count",
            new[] { "workflow", "state" });

    /// <summary>
    /// Time spent in each state histogram with workflow and state labels.
    /// Uses custom buckets for state duration timing.
    /// </summary>
    public static readonly Histogram StateDuration = Metrics
        .CreateHistogram("workflow_state_duration_seconds",
            "Time spent in each state",
            new[] { "workflow", "state" },
            new HistogramConfiguration
            {
                Buckets = new[] { 0.1, 1, 5, 10, 30, 60, 300, 600, 1800, 3600 }
            });
}