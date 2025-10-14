using System.Collections.Generic;
using System.Threading.Tasks;
using BBT.Workflow.Scripting.Functions;
using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Global functions available to all scripts
/// </summary>
public class GlobalScriptFunctions
{
    private readonly DaprSecretFunctions _secretFunctions;
    private readonly LoggingFunctions _loggingFunctions;
    private readonly ConfigurationFunctions _configurationFunctions;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalScriptFunctions"/> class
    /// </summary>
    /// <param name="daprClient">The Dapr client instance</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="configuration">The configuration instance</param>
    public GlobalScriptFunctions(DaprClient daprClient, ILogger logger, IConfiguration configuration)
    {
        _secretFunctions = new DaprSecretFunctions(daprClient);
        _loggingFunctions = new LoggingFunctions(logger);
        _configurationFunctions = new ConfigurationFunctions(configuration);
    }

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

    #region Logging Functions

    /// <summary>
    /// Logs a trace message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogTrace(string message)
    {
        _loggingFunctions.LogTrace(message);
    }

    /// <summary>
    /// Logs a debug message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogDebug(string message)
    {
        _loggingFunctions.LogDebug(message);
    }

    /// <summary>
    /// Logs an informational message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogInformation(string message)
    {
        _loggingFunctions.LogInformation(message);
    }

    /// <summary>
    /// Logs a warning message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogWarning(string message)
    {
        _loggingFunctions.LogWarning(message);
    }

    /// <summary>
    /// Logs an error message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogError(string message)
    {
        _loggingFunctions.LogError(message);
    }

    /// <summary>
    /// Logs a critical message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogCritical(string message)
    {
        _loggingFunctions.LogCritical(message);
    }

    #endregion

    #region Configuration Functions

    /// <summary>
    /// Gets a configuration value by key
    /// </summary>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <returns>The configuration value or null if not found</returns>
    public string? GetConfigValue(string key)
    {
        return _configurationFunctions.GetValue(key);
    }

    /// <summary>
    /// Gets a configuration value by key with a default value
    /// </summary>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <param name="defaultValue">The default value to return if key is not found</param>
    /// <returns>The configuration value or default value if not found</returns>
    public string GetConfigValue(string key, string defaultValue)
    {
        return _configurationFunctions.GetValue(key, defaultValue);
    }

    /// <summary>
    /// Gets a connection string by name
    /// </summary>
    /// <param name="name">The connection string name</param>
    /// <returns>The connection string or null if not found</returns>
    public string? GetConnectionString(string name)
    {
        return _configurationFunctions.GetConnectionString(name);
    }

    /// <summary>
    /// Checks if a configuration key exists
    /// </summary>
    /// <param name="key">The configuration key to check</param>
    /// <returns>True if the key exists, false otherwise</returns>
    public bool ConfigExists(string key)
    {
        return _configurationFunctions.Exists(key);
    }

    #endregion
} 