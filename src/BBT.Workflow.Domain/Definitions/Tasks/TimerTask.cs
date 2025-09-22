using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBT.Workflow.Definitions.Tasks;

public class TimerTask: WorkflowTask
{
    private TimerTask()
    {
        Type = ((int)TaskType.Timer).ToString();
    }
    
    [JsonConstructor]
    private TimerTask(
        JsonElement config) : base(config)
    {
        Type = ((int)TaskType.Timer).ToString();
    }

    public static TimerTask Create(
        JsonElement config)
    {
        return new TimerTask(config);
    }
    
    public static TimerTask Create()
    {
        return new TimerTask(new JsonElement());
    }

    public override WorkflowTask Clone()
    {
        return CloneTyped();
    }
    
    /// <summary>
    /// Creates a typed deep copy of the current ConditionTask instance.
    /// </summary>
    public TimerTask CloneTyped()
    {
        var cloned = new TimerTask();
        CopyBaseTo(cloned);
        
        return cloned;
    }

    /// <summary>
    /// Internal method for object pooling - copies all properties efficiently
    /// </summary>
    /// <param name="source">Source task to copy from</param>
    public void CopyFromInternal(TimerTask source)
    {
        source.CopyBaseToInternal(this);
        // ConditionTask has no additional properties beyond base
    }
    

    /// <summary>
    /// Creates a new instance for object pooling - internal use only
    /// </summary>
    public static TimerTask CreateEmpty()
    {
        return new TimerTask();
    }
}