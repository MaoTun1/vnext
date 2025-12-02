using System.Text.Json.Serialization;
using BBT.Aether.Domain.Values;

namespace BBT.Workflow.Definitions;

public sealed class ScriptCode : ValueObject
{
    public string Location { get; private set; }
    public string Code { get; private set; }
    public MappingType Type { get; private set; }

    private ScriptCode()
    {
    }

    [JsonConstructor]
    public ScriptCode(string location, string code, MappingType? type = null)
    {
        Location = location;
        Code = code;
        Type = type ?? MappingType.Local;
    }

    public string DecodedCode
    {
        get
        {
            try
            {
                if (!Type.Equals(MappingType.Global))
                {
                    var bytes = Convert.FromBase64String(Code);
                    return System.Text.Encoding.UTF8.GetString(bytes);
                }
                return string.Empty;
            }
            catch
            {
                throw new InvalidOperationException("Invalid Base64 string in ScriptCode.");
            }
        }
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Location;
        yield return Code;
        yield return Type;
    }
}