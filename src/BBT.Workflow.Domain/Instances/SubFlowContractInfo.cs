namespace BBT.Workflow.Instances;

/// <summary>
/// Contains parsed parent workflow information from SubFlow metadata
/// </summary>
public class SubFlowContractInfo
{
    public Guid Id { get; set; }
    public string? Key { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Flow { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? State { get; set; }
    public string SubType { get; set; } = string.Empty;
}