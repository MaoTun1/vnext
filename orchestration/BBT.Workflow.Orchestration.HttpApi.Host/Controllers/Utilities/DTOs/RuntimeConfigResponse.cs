namespace BBT.Workflow.Orchestration.Controllers.Utilities;

/// <summary>
/// Response model containing runtime configuration information for the workflow system.
/// </summary>
public sealed class RuntimeConfigResponse
{
    /// <summary>
    /// Gets or sets the current version of the workflow runtime system.
    /// </summary>
    /// <example>1.0.0</example>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the domain name that this runtime instance is configured to serve.
    /// </summary>
    /// <example>core</example>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the system schemas that this runtime.
    /// </summary>
    public Dictionary<string, string> Schemas { get; set; } = new();
}

