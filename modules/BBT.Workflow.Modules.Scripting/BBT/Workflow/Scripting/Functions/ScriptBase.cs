using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
    protected string GetSecret(string storeName, string secretStore, string secretKey)
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
    protected async Task<string> GetSecretAsync(string storeName, string secretStore, string secretKey)
    {
        return await ScriptHelper.GetSecretAsync(storeName, secretStore, secretKey);
    }

    /// <summary>
    /// Gets multiple secrets from Dapr secret store
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <returns>Dictionary of secret keys and values</returns>
    protected Dictionary<string, string> GetSecrets(string storeName, string secretStore)
    {
        return ScriptHelper.GetSecrets(storeName, secretStore);
    }

    /// <summary>
    /// Gets multiple secrets from Dapr secret store asynchronously
    /// </summary>
    /// <param name="storeName">The name of the dapr secret store</param>
    /// <param name="secretStore">The name of the secret store</param>
    /// <returns>Dictionary of secret keys and values</returns>
    protected async Task<Dictionary<string, string>> GetSecretsAsync(string storeName,
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
    protected bool HasProperty(object obj, string propertyName)
    {
        return ScriptHelper.HasProperty(obj, propertyName);
    }

    /// <summary>
    /// Gets a property value from a dynamic object safely
    /// </summary>
    /// <param name="obj">The dynamic object</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>The property value or null if not found</returns>
    protected object? GetPropertyValue(object obj, string propertyName)
    {
        return ScriptHelper.GetPropertyValue(obj, propertyName);
    }

    /// <summary>
    /// Gets a property value from a dynamic object safely and converts it to a specific type
    /// </summary>
    /// <typeparam name="T">The type to convert the value to</typeparam>
    /// <param name="obj">The dynamic object</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>The property value converted to type T or default(T) if not found or null</returns>
    protected T? GetPropertyValue<T>(object obj, string propertyName)
    {
        return ScriptHelper.GetPropertyValue<T>(obj, propertyName);
    }

    /// <summary>
    /// Gets a property value from a dynamic object safely and converts it to a specific type with a default value
    /// </summary>
    /// <typeparam name="T">The type to convert the value to</typeparam>
    /// <param name="obj">The dynamic object</param>
    /// <param name="propertyName">The property name</param>
    /// <param name="defaultValue">The default value to return if property is not found, null, or conversion fails</param>
    /// <returns>The property value converted to type T or defaultValue if not found, null, or conversion fails</returns>
    /// <example>
    /// <code>
    /// // Get string property with default
    /// var name = GetPropertyValue(context.Instance.Data, "name", "Unknown");
    /// 
    /// // Get int property with default
    /// var count = GetPropertyValue(context.Instance.Data, "count", 0);
    /// 
    /// // Get bool property with default
    /// var isActive = GetPropertyValue(context.Instance.Data, "isActive", false);
    /// </code>
    /// </example>
    protected T GetPropertyValue<T>(object obj, string propertyName, T defaultValue)
    {
        return ScriptHelper.GetPropertyValue(obj, propertyName, defaultValue);
    }

    #region Logging Functions

    /// <summary>
    /// Logs a trace message with caller information
    /// </summary>
    /// <param name="message">The message to log (supports structured logging placeholders like {propertyName})</param>
    /// <param name="file">Source file path (automatically captured)</param>
    /// <param name="method">Method name (automatically captured)</param>
    /// <param name="line">Line number (automatically captured)</param>
    /// <param name="args">Optional arguments for structured logging. IMPORTANT: Use named argument syntax: args: new object[] { value1, value2 }</param>
    /// <example>
    /// <code>
    /// // No arguments
    /// LogTrace("Processing started");
    /// 
    /// // With arguments - use named parameter!
    /// LogTrace("Status: {status}", args: new object[] { context.Instance?.Data?.status });
    /// 
    /// // Multiple arguments
    /// LogTrace("User {userId} at {time}", args: new object[] { userId, DateTime.Now });
    /// </code>
    /// </example>
    protected void LogTrace(
        string message,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? method = null,
        [CallerLineNumber] int line = 0,
        params object[] args)
    {
        ScriptHelper.LogTrace(message, string.IsNullOrEmpty(file) ? GetType().Name : file, method, line, args);
    }

    /// <summary>
    /// Logs a debug message with caller information
    /// </summary>
    /// <param name="message">The message to log (supports structured logging placeholders like {propertyName})</param>
    /// <param name="file">Source file path (automatically captured)</param>
    /// <param name="method">Method name (automatically captured)</param>
    /// <param name="line">Line number (automatically captured)</param>
    /// <param name="args">Optional arguments for structured logging. IMPORTANT: Use named argument syntax: args: new object[] { value1, value2 }</param>
    /// <example>
    /// <code>
    /// // No arguments
    /// LogDebug("Processing started");
    /// 
    /// // With arguments - use named parameter!
    /// LogDebug("Status: {status}", args: new object[] { context.Instance?.Data?.status });
    /// 
    /// // Multiple arguments
    /// LogDebug("User {userId} at {time}", args: new object[] { userId, DateTime.Now });
    /// </code>
    /// </example>
    protected void LogDebug(
        string message,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? method = null,
        [CallerLineNumber] int line = 0,
        params object[] args)
    {
        ScriptHelper.LogDebug(message, string.IsNullOrEmpty(file) ? GetType().Name : file, method, line, args);
    }

    /// <summary>
    /// Logs an informational message with caller information
    /// </summary>
    /// <param name="message">The message to log (supports structured logging placeholders like {propertyName})</param>
    /// <param name="file">Source file path (automatically captured)</param>
    /// <param name="method">Method name (automatically captured)</param>
    /// <param name="line">Line number (automatically captured)</param>
    /// <param name="args">Optional arguments for structured logging. IMPORTANT: Use named argument syntax: args: new object[] { value1, value2 }</param>
    /// <example>
    /// <code>
    /// // No arguments
    /// LogInformation("Processing started");
    /// 
    /// // With arguments - use named parameter!
    /// LogInformation("Status: {status}", args: new object[] { context.Instance?.Data?.status });
    /// 
    /// // Multiple arguments
    /// LogInformation("User {userId} at {time}", args: new object[] { userId, DateTime.Now });
    /// </code>
    /// </example>
    protected void LogInformation(
        string message,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? method = null,
        [CallerLineNumber] int line = 0,
        params object[] args)
    {
        ScriptHelper.LogInformation(message, string.IsNullOrEmpty(file) ? GetType().Name : file, method, line, args);
    }

    /// <summary>
    /// Logs a warning message with caller information
    /// </summary>
    /// <param name="message">The message to log (supports structured logging placeholders like {propertyName})</param>
    /// <param name="file">Source file path (automatically captured)</param>
    /// <param name="method">Method name (automatically captured)</param>
    /// <param name="line">Line number (automatically captured)</param>
    /// <param name="args">Optional arguments for structured logging. IMPORTANT: Use named argument syntax: args: new object[] { value1, value2 }</param>
    /// <example>
    /// <code>
    /// // No arguments
    /// LogWarning("Processing started");
    /// 
    /// // With arguments - use named parameter!
    /// LogWarning("Status: {status}", args: new object[] { context.Instance?.Data?.status });
    /// 
    /// // Multiple arguments
    /// LogWarning("User {userId} at {time}", args: new object[] { userId, DateTime.Now });
    /// </code>
    /// </example>
    protected void LogWarning(
        string message,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? method = null,
        [CallerLineNumber] int line = 0,
        params object[] args)
    {
        ScriptHelper.LogWarning(message, string.IsNullOrEmpty(file) ? GetType().Name : file, method, line, args);
    }

    /// <summary>
    /// Logs an error message with caller information
    /// </summary>
    /// <param name="message">The message to log (supports structured logging placeholders like {propertyName})</param>
    /// <param name="file">Source file path (automatically captured)</param>
    /// <param name="method">Method name (automatically captured)</param>
    /// <param name="line">Line number (automatically captured)</param>
    /// <param name="args">Optional arguments for structured logging. IMPORTANT: Use named argument syntax: args: new object[] { value1, value2 }</param>
    /// <example>
    /// <code>
    /// // No arguments
    /// LogError("Processing started");
    /// 
    /// // With arguments - use named parameter!
    /// LogError("Status: {status}", args: new object[] { context.Instance?.Data?.status });
    /// 
    /// // Multiple arguments
    /// LogError("User {userId} at {time}", args: new object[] { userId, DateTime.Now });
    /// </code>
    /// </example>
    protected void LogError(
        string message,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? method = null,
        [CallerLineNumber] int line = 0,
        params object[] args)
    {
        ScriptHelper.LogError(message, string.IsNullOrEmpty(file) ? GetType().Name : file, method, line, args);
    }

    /// <summary>
    /// Logs a critical message with caller information
    /// </summary>
    /// <param name="message">The message to log (supports structured logging placeholders like {propertyName})</param>
    /// <param name="file">Source file path (automatically captured)</param>
    /// <param name="method">Method name (automatically captured)</param>
    /// <param name="line">Line number (automatically captured)</param>
    /// <param name="args">Optional arguments for structured logging. IMPORTANT: Use named argument syntax: args: new object[] { value1, value2 }</param>
    /// <example>
    /// <code>
    /// // No arguments
    /// LogCritical("Processing started");
    /// 
    /// // With arguments - use named parameter!
    /// LogCritical("Status: {status}", args: new object[] { context.Instance?.Data?.status });
    /// 
    /// // Multiple arguments
    /// LogCritical("User {userId} at {time}", args: new object[] { userId, DateTime.Now });
    /// </code>
    /// </example>
    protected void LogCritical(
        string message,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? method = null,
        [CallerLineNumber] int line = 0,
        params object[] args)
    {
        ScriptHelper.LogCritical(message, string.IsNullOrEmpty(file) ? GetType().Name : file, method, line, args);
    }

    #endregion

    #region Configuration Functions

    /// <summary>
    /// Gets a configuration value by key
    /// </summary>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <returns>The configuration value or null if not found</returns>
    protected string? GetConfigValue(string key)
    {
        return ScriptHelper.GetConfigValue(key);
    }

    /// <summary>
    /// Gets a configuration value by key with a default value
    /// </summary>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <param name="defaultValue">The default value to return if key is not found</param>
    /// <returns>The configuration value or default value if not found</returns>
    protected string GetConfigValue(string key, string defaultValue)
    {
        return ScriptHelper.GetConfigValue(key, defaultValue);
    }

    /// <summary>
    /// Gets a configuration value as a specific type
    /// </summary>
    /// <typeparam name="T">The type to convert the value to</typeparam>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <returns>The configuration value converted to type T</returns>
    protected T? GetConfigValue<T>(string key)
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
    protected T GetConfigValue<T>(string key, T defaultValue)
    {
        return ScriptHelper.GetConfigValue<T>(key, defaultValue);
    }

    /// <summary>
    /// Gets a connection string by name
    /// </summary>
    /// <param name="name">The connection string name</param>
    /// <returns>The connection string or null if not found</returns>
    protected string? GetConnectionString(string name)
    {
        return ScriptHelper.GetConnectionString(name);
    }

    /// <summary>
    /// Checks if a configuration key exists
    /// </summary>
    /// <param name="key">The configuration key to check</param>
    /// <returns>True if the key exists, false otherwise</returns>
    protected bool ConfigExists(string key)
    {
        return ScriptHelper.ConfigExists(key);
    }

    #endregion
}