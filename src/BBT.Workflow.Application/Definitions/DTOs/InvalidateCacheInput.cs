namespace BBT.Workflow.Definitions;

public class InvalidateCacheInput
{
    public string Key { get; set; }
    public string Flow { get; set; }
    public string Domain { get; set; }
    public string Version { get; set; }
}