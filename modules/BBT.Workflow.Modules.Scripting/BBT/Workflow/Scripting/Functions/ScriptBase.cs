using System.Collections.Generic;
using System.Threading.Tasks;
using System.Dynamic;

namespace BBT.Workflow.Scripting.Functions;

/// <summary>
/// Base class for scripts that provides access to global functions
/// </summary>
public abstract class ScriptBase
{
    /// <summary>
    /// Gets a secret from Dapr secret store
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <param name="secretKey">The key of the secret</param>
    /// <returns>The secret value</returns>
    protected static string GetSecret(string storeName, string secretStore, string secretKey)
    {
        return ScriptHelper.GetSecret(storeName, secretStore, secretKey);
    }

    /// <summary>
    /// Gets a secret from Dapr secret store asynchronously
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <param name="secretKey">The key of the secret</param>
    /// <returns>The secret value</returns>
    protected static async Task<string> GetSecretAsync(string storeName, string secretStore, string secretKey)
    {
        return await ScriptHelper.GetSecretAsync(storeName, secretStore, secretKey);
    }

    /// <summary>
    /// Gets multiple secrets from Dapr secret store
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <returns>Dictionary of secret keys and values</returns>
    protected static Dictionary<string, string> GetSecrets(string storeName, string secretStore)
    {
        return ScriptHelper.GetSecrets(storeName, secretStore);
    }

    /// <summary>
    /// Gets multiple secrets from Dapr secret store asynchronously
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <returns>Dictionary of secret keys and values</returns>
    protected static async Task<Dictionary<string, string>> GetSecretsAsync(string storeName,
        string secretStore)
    {
        return await ScriptHelper.GetSecretsAsync(storeName, secretStore);
    }

    /// <summary>
    /// Checks if a dynamic object has a specific property
    /// </summary>
    /// <param name="obj">The dynamic object to check</param>
    /// <param name="propertyName">The property name to check for</param>
    /// <returns>True if the property exists, false otherwise</returns>
    protected static bool HasProperty(object obj, string propertyName)
    {
        return ScriptHelper.HasProperty(obj, propertyName);
    }

    /// <summary>
    /// Gets a property value from a dynamic object safely
    /// </summary>
    /// <param name="obj">The dynamic object</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>The property value or null if not found</returns>
    protected static object? GetPropertyValue(object obj, string propertyName)
    {
        return ScriptHelper.GetPropertyValue(obj, propertyName);
    }
}