using System.Text.Json;

namespace BBT.Workflow.BackgroundJobs.Payloads;

/// <summary>
/// Payload for asynchronous start instance background jobs.
/// Contains all necessary information to start a workflow instance in the background.
/// </summary>
public sealed class StartInstanceJobPayload
{
    /// <summary>
    /// Gets or sets the domain name for the workflow.
    /// </summary>
    public string Domain { get; set; } = default!;
    
    /// <summary>
    /// Gets or sets the workflow name.
    /// </summary>
    public string Workflow { get; set; } = default!;
    
    /// <summary>
    /// Gets or sets the workflow version.
    /// </summary>
    public string? Version { get; set; }
    
    /// <summary>
    /// Gets or sets the instance ID. If null, a new ID will be generated.
    /// </summary>
    public Guid? InstanceId { get; set; }
    
    /// <summary>
    /// Gets or sets the instance key.
    /// </summary>
    public string InstanceKey { get; set; } = default!;
    
    /// <summary>
    /// Gets or sets the instance tags.
    /// </summary>
    public string[]? Tags { get; set; }
    
    /// <summary>
    /// Gets or sets the instance attributes as JSON.
    /// </summary>
    public JsonElement? Attributes { get; set; }
    
    /// <summary>
    /// Gets or sets the callback URL.
    /// </summary>
    public string? Callback { get; set; }
    
    /// <summary>
    /// Gets or sets the instance metadata.
    /// </summary>
    public Dictionary<string, object> MetaData { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the request headers.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }
    
    /// <summary>
    /// Gets or sets the route values.
    /// </summary>
    public Dictionary<string, string?>? RouteValues { get; set; }
}
