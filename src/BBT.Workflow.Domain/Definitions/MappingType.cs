using System.Text.Json.Serialization;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Mapping type for script code execution (Global or Local)
/// </summary>
[JsonConverter(typeof(IEquatableJsonConverter<MappingType>))]
public sealed class MappingType : IEquatable<MappingType>
{
    public static readonly MappingType Global = new("G", "Global");
    public static readonly MappingType Local = new("L", "Local");

    public string Code { get; }
    public string Description { get; }

    private MappingType()
    {
    }

    private MappingType(string code, string description)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public static MappingType FromCode(string? code)
    {
        return code switch
        {
            "G" => Global,
            _ => Local
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is MappingType other && Equals(other);
    }

    public bool Equals(MappingType? other)
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

