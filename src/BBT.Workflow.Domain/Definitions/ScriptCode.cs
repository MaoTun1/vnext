using System.Text.Json.Serialization;
using BBT.Aether.Domain.Values;

namespace BBT.Workflow.Definitions;

public sealed class ScriptCode : ValueObject
{
    public string Location { get; private set; }
    public string Code { get; private set; }

    private ScriptCode()
    {
    }

    [JsonConstructor]
    public ScriptCode(string location, string code)
    {
        Location = location;
        Code = code;
    }

    public string DecodedCode
    {
        get
        {
            try
            {
                var bytes = Convert.FromBase64String(Code);
                return System.Text.Encoding.UTF8.GetString(bytes);
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
    }
}