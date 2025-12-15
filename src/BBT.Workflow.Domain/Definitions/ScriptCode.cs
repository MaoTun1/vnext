using System.Text.Json.Serialization;
using BBT.Aether.Domain.Values;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Represents a script code value object with support for both Base64 encoded and native (plain text) code content.
/// </summary>
public sealed class ScriptCode : ValueObject
{
    /// <summary>
    /// Default location value when none is provided.
    /// </summary>
    public const string DefaultLocation = "inline";

    /// <summary>
    /// The location/path identifier for the script.
    /// Defaults to "inline" when not specified.
    /// </summary>
    public string Location { get; private set; }

    /// <summary>
    /// The script code content. Can be Base64 encoded or native (plain text) based on <see cref="Encoding"/>.
    /// </summary>
    public string Code { get; private set; }

    /// <summary>
    /// The mapping type for script code execution (Global or Local).
    /// </summary>
    public MappingType Type { get; private set; }

    /// <summary>
    /// The encoding type of the code content. Base64 for backward compatibility, Native for plain text.
    /// </summary>
    public CodeEncoding Encoding { get; private set; }

    private ScriptCode()
    {
        Location = DefaultLocation;
        Code = string.Empty;
        Type = MappingType.Local;
        Encoding = CodeEncoding.Base64;
    }

    /// <summary>
    /// Creates a new ScriptCode instance.
    /// </summary>
    /// <param name="location">The location/path identifier. Defaults to "inline" if null or empty.</param>
    /// <param name="code">The script code content.</param>
    /// <param name="type">The mapping type (Global or Local). Defaults to Local.</param>
    /// <param name="encoding">The code encoding (Base64 or Native). Defaults to Base64 for backward compatibility.</param>
    [JsonConstructor]
    public ScriptCode(string? location, string? code, MappingType? type = null, CodeEncoding? encoding = null)
    {
        Location = string.IsNullOrWhiteSpace(location) ? DefaultLocation : location;
        Code = code ?? string.Empty;
        Type = type ?? MappingType.Local;
        Encoding = encoding ?? CodeEncoding.Base64;
    }

    /// <summary>
    /// Gets the decoded/usable script code content.
    /// For Base64 encoding, decodes the content. For Native encoding, returns the code as-is.
    /// Returns empty string for Global mapping type.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when Base64 decoding fails.</exception>
    public string DecodedCode
    {
        get
        {
            if (Type.Equals(MappingType.Global))
            {
                return string.Empty;
            }

            if (Encoding.Equals(CodeEncoding.Native))
            {
                return Code;
            }

            try
            {
                var bytes = Convert.FromBase64String(Code);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Invalid Base64 string in ScriptCode.", ex);
            }
        }
    }

    /// <summary>
    /// Creates a ScriptCode instance with native (plain text) encoding.
    /// </summary>
    /// <param name="code">The plain text script code.</param>
    /// <param name="location">Optional location identifier.</param>
    /// <param name="type">Optional mapping type.</param>
    /// <returns>A new ScriptCode instance with Native encoding.</returns>
    public static ScriptCode FromNative(string code, string? location = null, MappingType? type = null)
    {
        return new ScriptCode(location, code, type, CodeEncoding.Native);
    }

    /// <summary>
    /// Creates a ScriptCode instance with Base64 encoded content.
    /// </summary>
    /// <param name="base64Code">The Base64 encoded script code.</param>
    /// <param name="location">Optional location identifier.</param>
    /// <param name="type">Optional mapping type.</param>
    /// <returns>A new ScriptCode instance with Base64 encoding.</returns>
    public static ScriptCode FromBase64(string base64Code, string? location = null, MappingType? type = null)
    {
        return new ScriptCode(location, base64Code, type, CodeEncoding.Base64);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Location;
        yield return Code;
        yield return Type;
        yield return Encoding;
    }
}