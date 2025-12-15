using System.Text.Json.Serialization;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Encoding type for script code content (Base64 or Native/Plain text).
/// </summary>
[JsonConverter(typeof(IEquatableJsonConverter<CodeEncoding>))]
public sealed class CodeEncoding : IEquatable<CodeEncoding>
{
    /// <summary>
    /// Code content is Base64 encoded. Default for backward compatibility.
    /// </summary>
    public static readonly CodeEncoding Base64 = new("B64", "Base64");

    /// <summary>
    /// Code content is native/plain text (not encoded).
    /// </summary>
    public static readonly CodeEncoding Native = new("NAT", "Native");

    /// <summary>
    /// Short code identifier for the encoding type.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Human-readable description of the encoding type.
    /// </summary>
    public string Description { get; }

    private CodeEncoding()
    {
        Code = string.Empty;
        Description = string.Empty;
    }

    private CodeEncoding(string code, string description)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    /// <summary>
    /// Creates a CodeEncoding instance from its code representation.
    /// </summary>
    /// <param name="code">The encoding code (B64 or NAT).</param>
    /// <returns>The corresponding CodeEncoding instance.</returns>
    public static CodeEncoding FromCode(string? code)
    {
        return code switch
        {
            "B64" => Base64,
            "NAT" => Native,
            _ => Base64
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is CodeEncoding other && Equals(other);
    }

    public bool Equals(CodeEncoding? other)
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

