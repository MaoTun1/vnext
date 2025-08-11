using System.Text.Json.Serialization;

namespace BBT.Workflow.Definitions;

public sealed class OnExecuteTask
{
    private OnExecuteTask()
    {
    }

    [JsonConstructor]
    private OnExecuteTask(
        int order,
        Reference task,
        ScriptCode mapping
    )
    {
        Order = order;
        Task = task;
        Mapping = mapping;
    }

    public int Order { get; private set; }
    public Reference Task { get; private set; }
    public ScriptCode Mapping { get; private set; }

    public static OnExecuteTask Create(
        int order,
        IReference task,
        ScriptCode mapping)
    {
        return new OnExecuteTask(
            order,
            task.ToReference(),
            mapping);
    }
}