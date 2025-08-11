using System.Collections.Generic;
using System.Threading.Tasks;
using BBT.Workflow.Scripting.Functions;
using Dapr.Client;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Global functions available to all scripts
/// </summary>
public class GlobalScriptFunctions(DaprClient daprClient)
{
    private readonly DaprSecretFunctions _secretFunctions = new(daprClient);

    /// <summary>
    /// Gets a secret from Dapr secret store
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <param name="secretKey">The key of the secret</param>
    /// <returns>The secret value</returns>
    public string GetSecret(string storeName, string secretStore, string secretKey)
    {
        return _secretFunctions.GetSecret(storeName, secretStore, secretKey);
    }

    /// <summary>
    /// Gets multiple secrets from Dapr secret store
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <returns>Dictionary of secret keys and values</returns>
    public Dictionary<string, string> GetSecrets(string storeName, string secretStore)
    {
        return _secretFunctions.GetSecrets(storeName, secretStore);
    }

    /// <summary>
    /// Gets a secret from Dapr secret store asynchronously
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <param name="secretKey">The key of the secret</param>
    /// <returns>The secret value</returns>
    public async Task<string> GetSecretAsync(string storeName, string secretStore, string secretKey)
    {
        return await _secretFunctions.GetSecretAsync(storeName, secretStore, secretKey);
    }

    /// <summary>
    /// Gets multiple secrets from Dapr secret store asynchronously
    /// </summary>
    /// <param name="storeName">The name of the secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <returns>Dictionary of secret keys and values</returns>
    public async Task<Dictionary<string, string>> GetSecretsAsync(string storeName, string secretStore)
    {
        return await _secretFunctions.GetSecretsAsync(storeName, secretStore);
    }
} 