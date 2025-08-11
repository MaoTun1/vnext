using System.Text.Json;
using BBT.Workflow.Definitions;

public class GetInstanceRequest:PublishBaseInput
{
    public GetInstanceRequest()
    {

    }
    public string? IfNoneMatch { get; set; }
    public string[]? extensionRequested { get; set; }
    public int? Page{ get; set; }
    public int? PageSize{ get; set; }

}
