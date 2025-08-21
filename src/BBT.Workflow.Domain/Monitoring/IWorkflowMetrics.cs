namespace BBT.Workflow.Monitoring;

/// <summary>
/// Interface for workflow metrics collection.
/// Provides abstraction for monitoring workflow operations without coupling to specific monitoring implementation.
/// </summary>
public interface IWorkflowMetrics
{
    /// <summary>
    /// Records workflow instance creation.
    /// </summary>
    /// <param name="workflow">Workflow identifier</param>
    /// <param name="domain">Domain identifier</param>
    void RecordInstanceCreated(string workflow, string domain);

    /// <summary>
    /// Records workflow instance completion.
    /// </summary>
    /// <param name="workflow">Workflow identifier</param>
    /// <param name="domain">Domain identifier</param>
    /// <param name="durationSeconds">Total execution duration in seconds</param>
    void RecordInstanceCompleted(string workflow, string domain, double? durationSeconds = null);

    /// <summary>
    /// Records workflow instance timeout.
    /// </summary>
    /// <param name="workflow">Workflow identifier</param>
    /// <param name="domain">Domain identifier</param>
    /// <param name="currentStatus">Current status before timeout</param>
    /// <param name="durationSeconds">Total execution duration in seconds until timeout</param>
    void RecordInstanceTimedOut(string workflow, string domain, string currentStatus, double? durationSeconds = null);

    /// <summary>
    /// Records workflow instance duration.
    /// </summary>
    /// <param name="workflow">Workflow identifier</param>
    /// <param name="status">Instance completion status</param>
    /// <param name="durationSeconds">Duration in seconds</param>
    void RecordInstanceDuration(string workflow, string status, double durationSeconds);

    /// <summary>
    /// Updates active instances count.
    /// </summary>
    /// <param name="workflow">Workflow identifier</param>
    /// <param name="count">Current active count</param>
    void SetActiveInstances(string workflow, int count);

    /// <summary>
    /// Updates instance status metrics by transitioning from old status to new status.
    /// </summary>
    /// <param name="workflow">Workflow identifier</param>
    /// <param name="oldStatus">Previous instance status</param>
    /// <param name="newStatus">New instance status</param>
    void UpdateInstanceStatusMetrics(string workflow, string oldStatus, string newStatus);

    /// <summary>
    /// Records task execution start.
    /// </summary>
    /// <param name="taskType">Type of task executed</param>
    /// <param name="workflow">Workflow identifier</param>
    void RecordTaskExecuted(string taskType, string workflow);

    /// <summary>
    /// Records successful task completion.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    /// <param name="workflow">Workflow identifier</param>
    /// <param name="durationSeconds">Duration in seconds</param>
    void RecordTaskCompleted(string taskType, string workflow, double durationSeconds);

    /// <summary>
    /// Records task failure.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    /// <param name="workflow">Workflow identifier</param>
    /// <param name="durationSeconds">Duration in seconds until failure</param>
    void RecordTaskFailed(string taskType, string workflow, double durationSeconds);

    /// <summary>
    /// Records task retry attempt.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    /// <param name="workflow">Workflow identifier</param>
    void RecordTaskRetried(string taskType, string workflow);

    /// <summary>
    /// Records task execution duration.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    /// <param name="durationSeconds">Duration in seconds</param>
    void RecordTaskDuration(string taskType, double durationSeconds);

    /// <summary>
    /// Records task queue wait duration.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    /// <param name="waitDurationSeconds">Wait duration in seconds</param>
    void RecordTaskQueueWait(string taskType, double waitDurationSeconds);

    /// <summary>
    /// Increments pending tasks count when task is queued.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    /// <param name="workflow">Workflow identifier</param>
    void IncrementPendingTasks(string taskType, string workflow);

    /// <summary>
    /// Decrements pending tasks count and increments running tasks count when task starts execution.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    /// <param name="workflow">Workflow identifier</param>
    void StartTaskExecution(string taskType, string workflow);

    /// <summary>
    /// Decrements running tasks count when task finishes execution.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    /// <param name="workflow">Workflow identifier</param>
    void FinishTaskExecution(string taskType, string workflow);

    /// <summary>
    /// Updates task pool size.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    /// <param name="size">Current pool size</param>
    void SetTaskPoolSize(string taskType, int size);

    /// <summary>
    /// Records database query duration.
    /// </summary>
    /// <param name="queryType">Type of query</param>
    /// <param name="table">Target table</param>
    /// <param name="durationSeconds">Duration in seconds</param>
    void RecordDbQueryDuration(string queryType, string table, double durationSeconds);

    /// <summary>
    /// Records database transaction duration.
    /// </summary>
    /// <param name="operation">Transaction operation type</param>
    /// <param name="durationSeconds">Duration in seconds</param>
    void RecordDbTransactionDuration(string operation, double durationSeconds);

    /// <summary>
    /// Records database query execution.
    /// </summary>
    /// <param name="queryType">Type of query</param>
    /// <param name="table">Target table</param>
    /// <param name="status">Query execution status</param>
    void RecordDbQuery(string queryType, string table, string status);

    /// <summary>
    /// Records database operation error.
    /// </summary>
    /// <param name="operation">Database operation type</param>
    /// <param name="table">Target table</param>
    /// <param name="errorType">Type of error</param>
    void RecordDbError(string operation, string table, string errorType);

    /// <summary>
    /// Records database connection attempt.
    /// </summary>
    /// <param name="connectionType">Type of connection</param>
    /// <param name="status">Connection status</param>
    void RecordDbConnection(string connectionType, string status);

    /// <summary>
    /// Records cache hit.
    /// </summary>
    /// <param name="cacheName">Name of cache</param>
    void RecordCacheHit(string cacheName);

    /// <summary>
    /// Records cache miss.
    /// </summary>
    /// <param name="cacheName">Name of cache</param>
    void RecordCacheMiss(string cacheName);

    /// <summary>
    /// Records cache eviction.
    /// </summary>
    /// <param name="cacheName">Name of cache</param>
    /// <param name="reason">Eviction reason</param>
    void RecordCacheEviction(string cacheName, string reason);

    /// <summary>
    /// Sets current cache size in bytes.
    /// </summary>
    /// <param name="cacheName">Name of cache</param>
    /// <param name="sizeBytes">Cache size in bytes</param>
    void SetCacheSize(string cacheName, long sizeBytes);

    /// <summary>
    /// Sets number of cached entries.
    /// </summary>
    /// <param name="cacheName">Name of cache</param>
    /// <param name="entries">Number of cached entries</param>
    void SetCacheEntries(string cacheName, int entries);

    /// <summary>
    /// Records HTTP request.
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="endpoint">Endpoint path</param>
    /// <param name="statusCode">Response status code</param>
    void RecordHttpRequest(string method, string endpoint, string statusCode);

    /// <summary>
    /// Records HTTP request error.
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="endpoint">Endpoint path</param>
    /// <param name="errorType">Type of error</param>
    void RecordHttpError(string method, string endpoint, string errorType);

    /// <summary>
    /// Records HTTP request duration.
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="endpoint">Endpoint path</param>
    /// <param name="statusCode">Response status code</param>
    /// <param name="durationSeconds">Request duration in seconds</param>
    void RecordHttpRequestDuration(string method, string endpoint, string statusCode, double durationSeconds);

    /// <summary>
    /// Records HTTP response size.
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="endpoint">Endpoint path</param>
    /// <param name="statusCode">Response status code</param>
    /// <param name="sizeBytes">Response size in bytes</param>
    void RecordHttpResponseSize(string method, string endpoint, string statusCode, long sizeBytes);

    /// <summary>
    /// Records background job execution.
    /// </summary>
    /// <param name="jobType">Type of job</param>
    /// <param name="status">Job completion status</param>
    void RecordJobExecuted(string jobType, string status);

    /// <summary>
    /// Updates pending jobs count.
    /// </summary>
    /// <param name="jobType">Type of job</param>
    /// <param name="count">Current pending count</param>
    void SetJobsPending(string jobType, int count);

    /// <summary>
    /// Records error occurrence.
    /// </summary>
    /// <param name="errorType">Type of error</param>
    /// <param name="severity">Error severity level</param>
    /// <param name="component">Component where error occurred</param>
    void RecordError(string errorType, string severity, string component);

    /// <summary>
    /// Records state transition.
    /// </summary>
    /// <param name="workflow">Workflow identifier</param>
    /// <param name="fromState">Source state</param>
    /// <param name="toState">Target state</param>
    void RecordStateTransition(string workflow, string fromState, string toState);

    /// <summary>
    /// Records state entry.
    /// </summary>
    /// <param name="workflow">Workflow identifier</param>
    /// <param name="state">State being entered</param>
    void RecordStateEntry(string workflow, string state);

    /// <summary>
    /// Records state duration.
    /// </summary>
    /// <param name="workflow">Workflow identifier</param>
    /// <param name="state">State identifier</param>
    /// <param name="durationSeconds">Duration spent in state in seconds</param>
    void RecordStateDuration(string workflow, string state, double durationSeconds);

    /// <summary>
    /// Sets current pool size for a task type.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    /// <param name="size">Current pool size</param>
    void SetTaskFactoryPoolSize(string taskType, int size);

    /// <summary>
    /// Sets available objects count in pool for a task type.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    /// <param name="available">Available objects count</param>
    void SetTaskFactoryPoolAvailable(string taskType, int available);

    /// <summary>
    /// Sets objects currently in use count for a task type.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    /// <param name="inUse">Objects in use count</param>
    void SetTaskFactoryPoolInUse(string taskType, int inUse);

    /// <summary>
    /// Records object rental from pool.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    void RecordTaskFactoryPoolRental(string taskType);

    /// <summary>
    /// Records object return to pool.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    void RecordTaskFactoryPoolReturn(string taskType);

    /// <summary>
    /// Records new object creation for pool.
    /// </summary>
    /// <param name="taskType">Type of task</param>
    void RecordTaskFactoryPoolCreate(string taskType);
}