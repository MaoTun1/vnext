using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapr.Client;

namespace BBT.Workflow.Scripting.Functions;

/// <summary>
/// Custom functions for accessing Dapr secret store from scripts
/// </summary>
public class DaprSecretFunctions(DaprClient daprClient)
{
    private readonly DaprClient _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));

    /// <summary>
    /// Gets a secret from Dapr secret store
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <param name="secretKey">The key of the secret</param>
    /// <returns>The secret value</returns>
    public async Task<string> GetSecretAsync(string storeName, string secretStore, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentException("Dapr Store name cannot be null or empty", nameof(storeName));
        
        if (string.IsNullOrWhiteSpace(secretStore))
            throw new ArgumentException("Store name cannot be null or empty", nameof(secretStore));
        
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("Secret key cannot be null or empty", nameof(secretKey));

        try
        {
            var secretsResponse = await _daprClient.GetSecretAsync(storeName, secretStore);
            
            // Dapr returns secrets as a dictionary, get the first value
            return secretsResponse.TryGetValue(secretKey, out var value) ? value : string.Empty;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve secret '{secretKey}' from store '{storeName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets a secret from Dapr secret store (synchronous version for script compatibility)
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <param name="secretKey">The key of the secret</param>
    /// <returns>The secret value</returns>
    public string GetSecret(string storeName, string secretStore, string secretKey)
    {
        return GetSecretAsync(storeName, secretStore, secretKey).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets multiple secrets from Dapr secret store
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <returns>Dictionary of secret keys and values</returns>
    public async Task<Dictionary<string, string>> GetSecretsAsync(string storeName, string secretStore)
    {
        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentException("Dapr Store name cannot be null or empty", nameof(storeName));
        
        if (string.IsNullOrWhiteSpace(secretStore))
            throw new ArgumentException("Store name cannot be null or empty", nameof(secretStore));

        var secretsResponse = await _daprClient.GetSecretAsync(storeName, secretStore);
        return secretsResponse ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Gets multiple secrets from Dapr secret store (synchronous version)
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">he name of the secret store</param>
    /// <returns>Dictionary of secret keys and values</returns>
    public Dictionary<string, string> GetSecrets(string storeName, string secretStore)
    {
        return GetSecretsAsync(storeName, secretStore).GetAwaiter().GetResult();
    }
} 