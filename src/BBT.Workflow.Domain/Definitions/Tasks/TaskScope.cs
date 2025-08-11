using System.Text.Json.Serialization;

namespace BBT.Workflow.Definitions;

[JsonConverter(typeof(IEquatableJsonConverter<TaskScope>))]
public class TaskScope: IEquatable<TaskScope>
{
    public static readonly TaskScope Domain = new TaskScope("D", "Domain");
    public static readonly TaskScope Flow = new TaskScope("F", "Flow");
    public static readonly TaskScope Instance = new TaskScope("I", "Instance");

    public string Code { get; }
    public string Description { get; }

    private TaskScope()
    {
    }

    private TaskScope(string code, string description)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public static TaskScope FromCode(string code)
    {
        return code switch
        {
            "D" => Domain,
            "F" => Flow,
            "I" => Instance,
            _ => throw new ArgumentException($"Unknown status code: {code}")
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is TaskScope other && Equals(other);
    }

    public bool Equals(TaskScope? other)
    {
        return Code == other?.Code;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Code);
    }

    public override string ToString()
    {
        return $"{Description} ({Code})";
    }
}