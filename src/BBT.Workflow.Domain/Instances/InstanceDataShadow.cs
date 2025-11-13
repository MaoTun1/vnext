namespace BBT.Workflow.Instances;

public class InstanceDataShadow
{
    public Guid Id { get; set; }
    public Guid InstanceId { get; set; }
    public string Version { get; set; }
    public int HistorySequence { get; set; }
    public dynamic? Data { get; set; }

    internal InstanceData Map()
    {
        return new InstanceData(Id, InstanceId, Version, new JsonData(Data), true, HistorySequence);
    }
}
