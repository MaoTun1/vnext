using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBT.Workflow.Definitions.Tasks;

public class ConditionTask : WorkflowTask
{
    private ConditionTask()
    {
        Type = ((int)TaskType.Condition).ToString();
    }

    [JsonConstructor]
    private ConditionTask(
        JsonElement config) : base(config)
    {
        Type = ((int)TaskType.Condition).ToString();
    }

    public static ConditionTask Create(
        JsonElement config)
    {
        return new ConditionTask(config);
    }
    
    public static ConditionTask Create()
    {
        return new ConditionTask(new JsonElement());
    }

    public override WorkflowTask Clone()
    {
        return CloneTyped();
    }
    
    /// <summary>
    /// Creates a typed deep copy of the current ConditionTask instance.
    /// </summary>
    public ConditionTask CloneTyped()
    {
        var cloned = new ConditionTask();
        CopyBaseTo(cloned);
        
        return cloned;
    }

    /// <summary>
    /// Internal method for object pooling - copies all properties efficiently
    /// </summary>
    /// <param name="source">Source task to copy from</param>
    public void CopyFromInternal(ConditionTask source)
    {
        source.CopyBaseToInternal(this);
        // ConditionTask has no additional properties beyond base
    }
    

    /// <summary>
    /// Creates a new instance for object pooling - internal use only
    /// </summary>
    public static ConditionTask CreateEmpty()
    {
        return new ConditionTask();
    }
}