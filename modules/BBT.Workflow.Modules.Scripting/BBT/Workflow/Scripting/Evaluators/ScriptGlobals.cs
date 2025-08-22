using System.Collections.Generic;
using System.Threading.Tasks;
using System.Dynamic;
using BBT.Workflow.Scripting.Functions;

namespace BBT.Workflow.Scripting.Evaluators;

public class ScriptGlobals
{
    public dynamic Functions { get; set; }
    public dynamic Globals { get; set; }
    
    public string GetSecret(string store, string key) => Functions.GetSecret(store, key);
    public Task<string> GetSecretAsync(string store, string key) => Functions.GetSecretAsync(store, key);
    public Dictionary<string, string> GetSecrets(string store, params string[] keys) => Functions.GetSecrets(store, keys);
    public Task<Dictionary<string, string>> GetSecretsAsync(string store, params string[] keys) => Functions.GetSecretsAsync(store, keys);
    
    /// <summary>
    /// Checks if a dynamic object has a specific property
    /// </summary>
    /// <param name="obj">The dynamic object to check</param>
    /// <param name="propertyName">The property name to check for</param>
    /// <returns>True if the property exists, false otherwise</returns>
    public bool HasProperty(object obj, string propertyName) => ScriptHelper.HasProperty(obj, propertyName);
    
    /// <summary>
    /// Gets a property value from a dynamic object safely
    /// </summary>
    /// <param name="obj">The dynamic object</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>The property value or null if not found</returns>
    public object? GetPropertyValue(object obj, string propertyName) => ScriptHelper.GetPropertyValue(obj, propertyName);
}