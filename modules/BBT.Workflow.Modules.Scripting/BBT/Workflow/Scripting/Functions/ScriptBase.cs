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

    #region Logging Functions

    /// <summary>
    /// Logs a trace message
    /// </summary>
    /// <param name="message">The message to log</param>
    protected static void LogTrace(string message)
    {
        ScriptHelper.LogTrace(message);
    }

    /// <summary>
    /// Logs a debug message
    /// </summary>
    /// <param name="message">The message to log</param>
    protected static void LogDebug(string message)
    {
        ScriptHelper.LogDebug(message);
    }

    /// <summary>
    /// Logs an informational message
    /// </summary>
    /// <param name="message">The message to log</param>
    protected static void LogInformation(string message)
    {
        ScriptHelper.LogInformation(message);
    }

    /// <summary>
    /// Logs a warning message
    /// </summary>
    /// <param name="message">The message to log</param>
    protected static void LogWarning(string message)
    {
        ScriptHelper.LogWarning(message);
    }

    /// <summary>
    /// Logs an error message
    /// </summary>
    /// <param name="message">The message to log</param>
    protected static void LogError(string message)
    {
        ScriptHelper.LogError(message);
    }

    /// <summary>
    /// Logs a critical message
    /// </summary>
    /// <param name="message">The message to log</param>
    protected static void LogCritical(string message)
    {
        ScriptHelper.LogCritical(message);
    }

    #endregion

    #region Configuration Functions

    /// <summary>
    /// Gets a configuration value by key
    /// </summary>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <returns>The configuration value or null if not found</returns>
    protected static string? GetConfigValue(string key)
    {
        return ScriptHelper.GetConfigValue(key);
    }

    /// <summary>
    /// Gets a configuration value by key with a default value
    /// </summary>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <param name="defaultValue">The default value to return if key is not found</param>
    /// <returns>The configuration value or default value if not found</returns>
    protected static string GetConfigValue(string key, string defaultValue)
    {
        return ScriptHelper.GetConfigValue(key, defaultValue);
    }

    /// <summary>
    /// Gets a configuration value as a specific type
    /// </summary>
    /// <typeparam name="T">The type to convert the value to</typeparam>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <returns>The configuration value converted to type T</returns>
    protected static T? GetConfigValue<T>(string key)
    {
        return ScriptHelper.GetConfigValue<T>(key);
    }

    /// <summary>
    /// Gets a configuration value as a specific type with a default value
    /// </summary>
    /// <typeparam name="T">The type to convert the value to</typeparam>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <param name="defaultValue">The default value to return if key is not found</param>
    /// <returns>The configuration value converted to type T or default value if not found</returns>
    protected static T GetConfigValue<T>(string key, T defaultValue)
    {
        return ScriptHelper.GetConfigValue<T>(key, defaultValue);
    }

    /// <summary>
    /// Gets a connection string by name
    /// </summary>
    /// <param name="name">The connection string name</param>
    /// <returns>The connection string or null if not found</returns>
    protected static string? GetConnectionString(string name)
    {
        return ScriptHelper.GetConnectionString(name);
    }

    /// <summary>
    /// Checks if a configuration key exists
    /// </summary>
    /// <param name="key">The configuration key to check</param>
    /// <returns>True if the key exists, false otherwise</returns>
    protected static bool ConfigExists(string key)
    {
        return ScriptHelper.ConfigExists(key);
    }

    #endregion
}