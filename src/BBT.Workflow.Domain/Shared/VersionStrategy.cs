using System.Text.Json.Serialization;

namespace BBT.Workflow;

[JsonConverter(typeof(IEquatableJsonConverter<VersionStrategy>))]
public class VersionStrategy: IEquatable<VersionStrategy>
{
    public static readonly VersionStrategy None = new VersionStrategy("None", "None");
    public static readonly VersionStrategy IncreaseMinor = new VersionStrategy("Minor", "Increase Minor");
    public static readonly VersionStrategy IncreaseMajor = new VersionStrategy("Major", "Increase Minor");
    public static readonly VersionStrategy IncreasePatch = new VersionStrategy("Patch", "Increase Patch");

    public string Code { get; }
    public string Description { get; }

    private VersionStrategy()
    {
    }

    private VersionStrategy(string code, string description)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public static VersionStrategy FromCode(string code)
    {
        return code switch
        {
            "None" => None,
            "Minor" => IncreaseMinor,
            "Major" => IncreaseMajor,
            "Patch" => IncreasePatch,
            _ => throw new ArgumentException($"Unknown status code: {code}")
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is VersionStrategy other && Equals(other);
    }

    public bool Equals(VersionStrategy? other)
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