using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBT.Workflow.Definitions;

public sealed class ScriptTask : WorkflowTask
{
    private ScriptTask()
    {
    }

    [JsonConstructor]
    private ScriptTask(
        JsonElement config) : base(config)
    {
        Type = ((int)TaskType.Script).ToString();
    }

    public static ScriptTask Create(
        JsonElement config)
    {
        return new ScriptTask(config);
    }

    public override WorkflowTask Clone()
    {
        return CloneTyped();
    }
    
    /// <summary>
    /// Creates a typed deep copy of the current ScriptTask instance.
    /// </summary>
    public ScriptTask CloneTyped()
    {
        var cloned = new ScriptTask();
        CopyBaseTo(cloned);
        
        return cloned;
    }

    /// <summary>
    /// Internal method for object pooling - copies all properties efficiently
    /// </summary>
    /// <param name="source">Source task to copy from</param>
    public void CopyFromInternal(ScriptTask source)
    {
        source.CopyBaseToInternal(this);
        // ScriptTask has no additional properties beyond base
    }

    /// <summary>
    /// Creates a new instance for object pooling - internal use only
    /// </summary>
    public static ScriptTask CreateEmpty()
    {
        return new ScriptTask();
    }
}