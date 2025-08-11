using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapr.Client;

namespace BBT.Workflow.Scripting.Functions;

/// <summary>
/// Static helper class for script functions that don't require dependency injection
/// </summary>
public static class ScriptHelper
{
    private static DaprClient? _daprClient;

    /// <summary>
    /// Sets the Dapr client instance (should be called during application startup)
    /// </summary>
    /// <param name="daprClient">The Dapr client instance</param>
    public static void SetDaprClient(DaprClient daprClient)
    {
        _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
    }

    /// <summary>
    /// Gets a secret from Dapr secret store
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <param name="secretKey">The key of the secret</param>
    /// <returns>The secret value</returns>
    public static string GetSecret(string storeName, string secretStore, string secretKey)
    {
        if (_daprClient == null)
            throw new InvalidOperationException("Dapr client is not initialized. Call SetDaprClient first.");

        return GetSecretAsync(storeName, secretStore, secretKey).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets a secret from Dapr secret store asynchronously
    /// </summary>
    /// <param name="storeName">The name of the dapr secret name</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <param name="secretKey">The key of the secret</param>
    /// <returns>The secret value</returns>
    public static async Task<string> GetSecretAsync(string storeName, string secretStore, string secretKey)
    {
        if (_daprClient == null)
            throw new InvalidOperationException("Dapr client is not initialized. Call SetDaprClient first.");

        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentException("Dapr Store name cannot be null or empty", nameof(storeName));
        
        if (string.IsNullOrWhiteSpace(secretStore))
            throw new ArgumentException("Store name cannot be null or empty", nameof(secretStore));

        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("Secret key cannot be null or empty", nameof(secretKey));

        try
        {
            var secretsResponse = await _daprClient.GetSecretAsync(storeName, secretStore);

            if (secretsResponse == null)
                return string.Empty;
            // Dapr returns secrets as a dictionary, get the first value
            return secretsResponse.TryGetValue(secretKey, out var value) ? value : string.Empty;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to retrieve secret '{secretKey}' from store '{storeName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets multiple secrets from Dapr secret store
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <returns>Dictionary of secret keys and values</returns>
    public static Dictionary<string, string> GetSecrets(string storeName, string secretStore)
    {
        return GetSecretsAsync(storeName,  secretStore).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets multiple secrets from Dapr secret store asynchronously
    /// </summary>
    /// <param name="storeName">The name of the secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <returns>Dictionary of secret keys and values</returns>
    public static async Task<Dictionary<string, string>> GetSecretsAsync(string storeName, string secretStore)
    {
        if (_daprClient == null)
            throw new InvalidOperationException("Dapr client is not initialized. Call SetDaprClient first.");
        
        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentException("Dapr Store name cannot be null or empty", nameof(storeName));

        if (string.IsNullOrWhiteSpace(secretStore))
            throw new ArgumentException("Store name cannot be null or empty", nameof(storeName));
        
        var secretsResponse = await _daprClient.GetSecretAsync(storeName, secretStore);
        return secretsResponse ?? new Dictionary<string, string>();
    }
}