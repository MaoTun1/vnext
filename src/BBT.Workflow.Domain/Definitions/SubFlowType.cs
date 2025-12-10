using System.Text.Json.Serialization;

namespace BBT.Workflow.Definitions;

[JsonConverter(typeof(IEquatableJsonConverter<SubFlowType>))]
public sealed class SubFlowType: IEquatable<SubFlowType>
{
    public static readonly SubFlowType SubFlow = new("S", "Sub Flow");
    public static readonly SubFlowType SubProcess = new("P", "Sub Process");

    public string Code { get; }
    public string Description { get; }

    private SubFlowType()
    {
    }

    private SubFlowType(string code, string description)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public static SubFlowType FromCode(string code)
    {
        return code switch
        {
            "S" => SubFlow,
            "P" => SubProcess,
            _ => throw new ArgumentException($"Unknown workflow type code: {code}")
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is SubFlowType other && Equals(other);
    }

    public bool Equals(SubFlowType? other)
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