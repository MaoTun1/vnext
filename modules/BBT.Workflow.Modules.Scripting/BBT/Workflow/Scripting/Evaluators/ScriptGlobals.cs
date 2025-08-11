using System.Collections.Generic;
using System.Threading.Tasks;

namespace BBT.Workflow.Scripting.Evaluators;

public class ScriptGlobals
{
    public dynamic Functions { get; set; }
    public dynamic Globals { get; set; }
    
    public string GetSecret(string store, string key) => Functions.GetSecret(store, key);
    public Task<string> GetSecretAsync(string store, string key) => Functions.GetSecretAsync(store, key);
    public Dictionary<string, string> GetSecrets(string store, params string[] keys) => Functions.GetSecrets(store, keys);
    public Task<Dictionary<string, string>> GetSecretsAsync(string store, params string[] keys) => Functions.GetSecretsAsync(store, keys);
}