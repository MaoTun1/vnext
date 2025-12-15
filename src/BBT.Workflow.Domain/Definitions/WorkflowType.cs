using System.Text.Json.Serialization;

namespace BBT.Workflow.Definitions;

[JsonConverter(typeof(IEquatableJsonConverter<WorkflowType>))]
public sealed class WorkflowType: IEquatable<WorkflowType>
{
    public static readonly WorkflowType Core = new("C", "Core");
    public static readonly WorkflowType Flow = new("F", "Flow");
    public static readonly WorkflowType SubFlow = new("S", "Sub Flow");
    public static readonly WorkflowType SubProcess = new("P", "Sub Process");

    public string Code { get; }
    public string Description { get; }

    private WorkflowType()
    {
    }

    private WorkflowType(string code, string description)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public static WorkflowType FromCode(string code)
    {
        return code switch
        {
            "C" => Core,
            "F" => Flow,
            "S" => SubFlow,
            "P" => SubProcess,
            _ => throw new ArgumentException($"Unknown workflow type code: {code}")
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is WorkflowType other && Equals(other);
    }

    public bool Equals(WorkflowType? other)
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