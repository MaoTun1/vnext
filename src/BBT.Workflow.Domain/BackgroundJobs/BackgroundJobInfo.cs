namespace BBT.Workflow.BackgroundJobs;

/// <summary>
/// Represents comprehensive information about a background job including its configuration,
/// payload, metadata, and execution state. This class encapsulates all necessary data
/// for job persistence, scheduling, and execution tracking.
/// </summary>
/// <typeparam name="T">The type of the job payload. Must be a reference type.</typeparam>
public class BackgroundJobInfo<T> where T : class
{
    /// <summary>
    /// Gets or sets the name of the job type. This identifier is used to route
    /// the job to the appropriate handler during execution.
    /// </summary>
    /// <value>A string representing the job type name.</value>
    public string JobName { get; set; } = default!;
    
    /// <summary>
    /// Gets or sets the unique identifier for this specific job instance.
    /// This ID is used for job tracking, cancellation, and duplicate prevention.
    /// </summary>
    /// <value>A unique string identifier for the job instance.</value>
    public string JobId { get; set; } = default!;
    
    /// <summary>
    /// Gets or sets the schedule expression value that determines when the job should be executed.
    /// This typically contains cron expressions or time-based scheduling information.
    /// </summary>
    /// <value>A string representing the scheduling expression.</value>
    public string ExpressionValue { get; set; }
    
    /// <summary>
    /// Gets or sets the job payload containing the data to be processed by the job handler.
    /// </summary>
    /// <value>An instance of type T containing the job's data payload.</value>
    public T Payload { get; set; } = default!;
    
    /// <summary>
    /// Gets or sets additional metadata associated with the job.
    /// Common metadata includes domain, flow name, and instance ID information.
    /// </summary>
    /// <value>A dictionary containing key-value pairs of metadata, or null if no metadata is present.</value>
    public Dictionary<string, string>? Metadata { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether this job has been triggered for execution.
    /// This flag helps track job execution state and prevent duplicate processing.
    /// </summary>
    /// <value>True if the job has been triggered; otherwise, false.</value>
    public bool IsTriggered { get; set; }

    /// <summary>
    /// Retrieves the domain value from the job's metadata.
    /// </summary>
    /// <returns>
    /// The domain string if found in metadata; otherwise, an empty string.
    /// Returns an empty string if metadata is null or the domain key is not found.
    /// </returns>
    public string GetDomain()
    {
        if (Metadata == null)
            return string.Empty;

        if (Metadata.TryGetValue("domain", out var domain) && !string.IsNullOrWhiteSpace(domain))
            return domain;
        
        if (Metadata.TryGetValue("Domain", out var altDomain) && !string.IsNullOrWhiteSpace(altDomain))
            return altDomain;
        
        return string.Empty;
    }

    /// <summary>
    /// Retrieves the flow name value from the job's metadata.
    /// </summary>
    /// <returns>
    /// The flow name string if found in metadata; otherwise, an empty string.
    /// Returns an empty string if metadata is null or the flowName key is not found.
    /// </returns>
    public string GetFlowName()
    {
        if (Metadata == null)
            return string.Empty;
        
        if (Metadata.TryGetValue("flowName", out var flowName) && !string.IsNullOrWhiteSpace(flowName))
            return flowName;
        
        if (Metadata.TryGetValue("FlowName", out var altFlowName) && !string.IsNullOrWhiteSpace(altFlowName))
            return altFlowName;
        
        return string.Empty;
    }

    /// <summary>
    /// Retrieves and parses the instance ID value from the job's metadata.
    /// </summary>
    /// <returns>
    /// A valid Guid representing the instance ID if found and parseable; otherwise, Guid.Empty.
    /// Returns Guid.Empty if metadata is null, the instanceId key is not found, or parsing fails.
    /// </returns>
    public Guid GetInstanceId()
    {
        if (Metadata == null)
            return Guid.Empty;
        
        if (Metadata.TryGetValue("instanceId", out var instanceId) && !string.IsNullOrWhiteSpace(instanceId))
            return Guid.Parse(instanceId);

        if (Metadata.TryGetValue("InstanceId", out var altInstanceId) && !string.IsNullOrWhiteSpace(altInstanceId))
            return Guid.Parse(altInstanceId);

        return Guid.Empty;
    }
}