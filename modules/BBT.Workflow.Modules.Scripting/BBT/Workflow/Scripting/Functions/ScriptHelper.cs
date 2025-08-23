using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
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

    /// <summary>
    /// Checks if a dynamic object has a specific property
    /// </summary>
    /// <param name="obj">The dynamic object to check</param>
    /// <param name="propertyName">The property name to check for</param>
    /// <returns>True if the property exists, false otherwise</returns>
    public static bool HasProperty(object obj, string propertyName)
    {
        if (obj == null) return false;
        
        // Check if it's an ExpandoObject
        if (obj is ExpandoObject expando)
        {
            return ((IDictionary<string, object>)expando).ContainsKey(propertyName);
        }
        
        // Check if it's another IDictionary<string, object>
        if (obj is IDictionary<string, object> dictionary)
        {
            return dictionary.ContainsKey(propertyName);
        }
        
        // For other dynamic objects, try to access the property using reflection
        var objType = obj.GetType();
        return objType.GetProperty(propertyName) != null || 
               objType.GetField(propertyName) != null;
    }
    
    /// <summary>
    /// Gets a property value from a dynamic object safely
    /// </summary>
    /// <param name="obj">The dynamic object</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>The property value or null if not found</returns>
    public static object? GetPropertyValue(object obj, string propertyName)
    {
        if (obj == null) return null;
        
        // Check if it's an ExpandoObject
        if (obj is ExpandoObject expando)
        {
            IDictionary<string, object> dict = (IDictionary<string, object>)expando;
            return dict.TryGetValue(propertyName, out var value) ? value : null;
        }
        
        // Check if it's another IDictionary<string, object>
        if (obj is IDictionary<string, object> dictionary)
        {
            return dictionary.TryGetValue(propertyName, out var value) ? value : null;
        }
        
        // For other dynamic objects, try to access the property using reflection
        var objType = obj.GetType();
        var property = objType.GetProperty(propertyName);
        if (property != null)
        {
            return property.GetValue(obj);
        }
        
        var field = objType.GetField(propertyName);
        if (field != null)
        {
            return field.GetValue(obj);
        }
        
        return null;
    }
}