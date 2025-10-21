using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Scripting.Functions;

/// <summary>
/// Static helper class for script functions that don't require dependency injection
/// </summary>
public static class ScriptHelper
{
    private static DaprClient? _daprClient;
    private static ILogger? _logger;
    private static IConfiguration? _configuration;

    /// <summary>
    /// Sets the Dapr client instance (should be called during application startup)
    /// </summary>
    /// <param name="daprClient">The Dapr client instance</param>
    public static void SetDaprClient(DaprClient daprClient)
    {
        _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
    }

    /// <summary>
    /// Sets the logger instance (should be called during application startup)
    /// </summary>
    /// <param name="logger">The logger instance</param>
    public static void SetLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sets the configuration instance (should be called during application startup)
    /// </summary>
    /// <param name="configuration">The configuration instance</param>
    public static void SetConfiguration(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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
        return GetSecretsAsync(storeName, secretStore).GetAwaiter().GetResult();
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
        if (obj == null || string.IsNullOrWhiteSpace(propertyName)) return false;

        // Check if it's an ExpandoObject
        if (obj is IDictionary<string, object> dict)
        {
            return dict.ContainsKey(propertyName);
        }

        // Check if it's another IDictionary<string, object>
        if (obj is IDictionary<string, object> dictionary)
        {
            return dictionary.ContainsKey(propertyName);
        }

        // For other dynamic objects, try to access the property using reflection
        var objType = obj.GetType();
        return objType.GetProperty(propertyName,
                   System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                   System.Reflection.BindingFlags.IgnoreCase) != null ||
               objType.GetField(propertyName,
                   System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                   System.Reflection.BindingFlags.IgnoreCase) != null;
    }

    /// <summary>
    /// Gets a property value from a dynamic object safely
    /// </summary>
    /// <param name="obj">The dynamic object</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>The property value or null if not found</returns>
    public static object? GetPropertyValue(object obj, string propertyName)
    {
        if (obj == null || string.IsNullOrWhiteSpace(propertyName)) return null;

        if (obj is IDictionary<string, object> dict)
        {
            return dict.TryGetValue(propertyName, out var value) ? value : null;
        }

        var objType = obj.GetType();
        var property = objType.GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.IgnoreCase);
        if (property != null)
        {
            return property.GetValue(obj);
        }
        
        var field = objType.GetField(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.IgnoreCase);
        if (field != null)
        {
            return field.GetValue(obj);
        }

        return null;
    }

    #region Logging Functions

    /// <summary>
    /// Logs a trace message with caller information
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="file">The source file path (automatically captured)</param>
    /// <param name="method">The method name (automatically captured)</param>
    /// <param name="line">The line number (automatically captured)</param>
    /// <param name="args">Optional arguments for string formatting</param>
    public static void LogTrace(
        object message,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? method = null,
        [CallerLineNumber] int line = 0,
        params object[] args)
    {
        if (_logger == null)
            throw new InvalidOperationException("Logger is not initialized. Call SetLogger first.");

        if (message == null)
            return;

        var enrichedMessage = $"Class: {{File}} Method: {{Method}} Line: {{Line}} Message: {message}";
        var logArgs = new List<object> { file ?? "Unknown", method ?? "Unknown", line };
        if (args != null && args.Length > 0)
            logArgs.AddRange(args);

        _logger.LogTrace(enrichedMessage, logArgs.ToArray());
    }

    /// <summary>
    /// Logs a debug message with caller information
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="file">The source file path (automatically captured)</param>
    /// <param name="method">The method name (automatically captured)</param>
    /// <param name="line">The line number (automatically captured)</param>
    /// <param name="args">Optional arguments for string formatting</param>
    public static void LogDebug(
        object message,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? method = null,
        [CallerLineNumber] int line = 0,
        params object[] args)
    {
        if (_logger == null)
            throw new InvalidOperationException("Logger is not initialized. Call SetLogger first.");

        if (message == null)
            return;

        var enrichedMessage = $"Class: {{File}} Method: {{Method}} Line: {{Line}} Message: {message}";
        var logArgs = new List<object> { file ?? "Unknown", method ?? "Unknown", line };
        if (args != null && args.Length > 0)
            logArgs.AddRange(args);

        _logger.LogDebug(enrichedMessage, logArgs.ToArray());
    }

    /// <summary>
    /// Logs an informational message with caller information
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="file">The source file path (automatically captured)</param>
    /// <param name="method">The method name (automatically captured)</param>
    /// <param name="line">The line number (automatically captured)</param>
    /// <param name="args">Optional arguments for string formatting</param>
    public static void LogInformation(
        object message,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? method = null,
        [CallerLineNumber] int line = 0,
        params object[] args)
    {
        if (_logger == null)
            throw new InvalidOperationException("Logger is not initialized. Call SetLogger first.");

        if (message == null)
            return;

        var enrichedMessage = $"Class: {{File}} Method: {{Method}} Line: {{Line}} Message: {message}";
        var logArgs = new List<object> { file ?? "Unknown", method ?? "Unknown", line };
        if (args != null && args.Length > 0)
            logArgs.AddRange(args);

        _logger.LogInformation(enrichedMessage, logArgs.ToArray());
    }

    /// <summary>
    /// Logs a warning message with caller information
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="file">The source file path (automatically captured)</param>
    /// <param name="method">The method name (automatically captured)</param>
    /// <param name="line">The line number (automatically captured)</param>
    /// <param name="args">Optional arguments for string formatting</param>
    public static void LogWarning(
        object message,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? method = null,
        [CallerLineNumber] int line = 0,
        params object[] args)
    {
        if (_logger == null)
            throw new InvalidOperationException("Logger is not initialized. Call SetLogger first.");

        if (message == null)
            return;

        var enrichedMessage = $"Class: {{File}} Method: {{Method}} Line: {{Line}} Message: {message}";
        var logArgs = new List<object> { file ?? "Unknown", method ?? "Unknown", line };
        if (args != null && args.Length > 0)
            logArgs.AddRange(args);

        _logger.LogWarning(enrichedMessage, logArgs.ToArray());
    }

    /// <summary>
    /// Logs an error message with caller information
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="file">The source file path (automatically captured)</param>
    /// <param name="method">The method name (automatically captured)</param>
    /// <param name="line">The line number (automatically captured)</param>
    /// <param name="args">Optional arguments for string formatting</param>
    public static void LogError(
        object message,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? method = null,
        [CallerLineNumber] int line = 0,
        params object[] args)
    {
        if (_logger == null)
            throw new InvalidOperationException("Logger is not initialized. Call SetLogger first.");

        if (message == null)
            return;

        var enrichedMessage = $"Class: {{File}} Method: {{Method}} Line: {{Line}} Message: {message}";
        var logArgs = new List<object> { file ?? "Unknown", method ?? "Unknown", line };
        if (args != null && args.Length > 0)
            logArgs.AddRange(args);

        _logger.LogError(enrichedMessage, logArgs.ToArray());
    }

    /// <summary>
    /// Logs a critical message with caller information
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="file">The source file path (automatically captured)</param>
    /// <param name="method">The method name (automatically captured)</param>
    /// <param name="line">The line number (automatically captured)</param>
    /// <param name="args">Optional arguments for string formatting</param>
    public static void LogCritical(
        object message,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? method = null,
        [CallerLineNumber] int line = 0,
        params object[] args)
    {
        if (_logger == null)
            throw new InvalidOperationException("Logger is not initialized. Call SetLogger first.");

        if (message == null)
            return;

        var enrichedMessage = $"Class: {{File}} Method: {{Method}} Line: {{Line}} Message: {message}";
        var logArgs = new List<object> { file ?? "Unknown", method ?? "Unknown", line };
        if (args != null && args.Length > 0)
            logArgs.AddRange(args);

        _logger.LogCritical(enrichedMessage, logArgs.ToArray());
    }

    #endregion

    #region Configuration Functions

    /// <summary>
    /// Gets a configuration value by key
    /// </summary>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <returns>The configuration value or null if not found</returns>
    public static string? GetConfigValue(string key)
    {
        if (_configuration == null)
            throw new InvalidOperationException("Configuration is not initialized. Call SetConfiguration first.");

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        return _configuration[key];
    }

    /// <summary>
    /// Gets a configuration value by key with a default value
    /// </summary>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <param name="defaultValue">The default value to return if key is not found</param>
    /// <returns>The configuration value or default value if not found</returns>
    public static string GetConfigValue(string key, string defaultValue)
    {
        if (_configuration == null)
            throw new InvalidOperationException("Configuration is not initialized. Call SetConfiguration first.");

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        return _configuration[key] ?? defaultValue;
    }

    /// <summary>
    /// Gets a configuration value as a specific type
    /// </summary>
    /// <typeparam name="T">The type to convert the value to</typeparam>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <returns>The configuration value converted to type T</returns>
    public static T? GetConfigValue<T>(string key)
    {
        if (_configuration == null)
            throw new InvalidOperationException("Configuration is not initialized. Call SetConfiguration first.");

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        var value = _configuration[key];
        if (value == null)
            return default;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Gets a configuration value as a specific type with a default value
    /// </summary>
    /// <typeparam name="T">The type to convert the value to</typeparam>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <param name="defaultValue">The default value to return if key is not found</param>
    /// <returns>The configuration value converted to type T or default value if not found</returns>
    public static T GetConfigValue<T>(string key, T defaultValue)
    {
        if (_configuration == null)
            throw new InvalidOperationException("Configuration is not initialized. Call SetConfiguration first.");

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or empty", nameof(key));

        var value = _configuration[key];
        if (value == null)
            return defaultValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Gets a connection string by name
    /// </summary>
    /// <param name="name">The connection string name</param>
    /// <returns>The connection string or null if not found</returns>
    public static string? GetConnectionString(string name)
    {
        if (_configuration == null)
            throw new InvalidOperationException("Configuration is not initialized. Call SetConfiguration first.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Connection string name cannot be null or empty", nameof(name));

        return _configuration.GetConnectionString(name);
    }

    /// <summary>
    /// Checks if a configuration key exists
    /// </summary>
    /// <param name="key">The configuration key to check</param>
    /// <returns>True if the key exists, false otherwise</returns>
    public static bool ConfigExists(string key)
    {
        if (_configuration == null)
            throw new InvalidOperationException("Configuration is not initialized. Call SetConfiguration first.");

        if (string.IsNullOrWhiteSpace(key))
            return false;

        return _configuration[key] != null;
    }

    #endregion

    #region Testing Support

    /// <summary>
    /// Resets all static instances. This method should only be used in testing scenarios.
    /// </summary>
    public static void Reset()
    {
        _daprClient = null;
        _logger = null;
        _configuration = null;
    }

    #endregion
}