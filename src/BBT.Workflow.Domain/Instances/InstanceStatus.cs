using System.Text.Json.Serialization;

namespace BBT.Workflow.Instances;

[JsonConverter(typeof(IEquatableJsonConverter<InstanceStatus>))]
public sealed class InstanceStatus : IEquatable<InstanceStatus>
{
    public static readonly InstanceStatus Busy = new InstanceStatus("B", "Busy");
    public static readonly InstanceStatus Active = new InstanceStatus("A", "Active");
    public static readonly InstanceStatus Passive = new InstanceStatus("P", "Passive");
    public static readonly InstanceStatus Completed = new InstanceStatus("C", "Completed");
    public static readonly InstanceStatus Faulted = new InstanceStatus("F", "Faulted");
    public static readonly InstanceStatus Canceled = new InstanceStatus("X", "Canceled");

    public string Code { get; }
    public string Description { get; }

    private InstanceStatus()
    {
    }

    private InstanceStatus(string code, string description)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public static InstanceStatus FromCode(string code)
    {
        return code switch
        {
            "B" => Busy,
            "A" => Active,
            "P" => Passive,
            "C" => Completed,
            "F" => Faulted,
            "X" => Canceled,
            _ => throw new ArgumentException($"Unknown status code: {code}")
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is InstanceStatus other && Equals(other);
    }

    public bool Equals(InstanceStatus? other)
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