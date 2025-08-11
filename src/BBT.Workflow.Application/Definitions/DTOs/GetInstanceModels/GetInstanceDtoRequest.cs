using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;

public class GetInstanceDtoRequest
{
    public GetInstanceDtoRequest()
    {

    }
    public string Domain { get; set; }
    public string Flow { get; set; }
    public string[]? extensionRequested { get; set; }
    public Instance InstanceModel{ get; set; }
    public InstanceData? InstanceData{ get; set; }
    public ExtensionScope ExtensionScope { get; set; }
}
