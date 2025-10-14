using System;
using Microsoft.Extensions.Configuration;

namespace BBT.Workflow.Scripting.Functions;

/// <summary>
/// Custom functions for accessing configuration from scripts
/// </summary>
public class ConfigurationFunctions
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationFunctions"/> class
    /// </summary>
    /// <param name="configuration">The configuration instance</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null</exception>
    public ConfigurationFunctions(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Gets a configuration value by key
    /// </summary>
    /// <param name="key">The configuration key (supports nested keys with ':' separator)</param>
    /// <returns>The configuration value or null if not found</returns>
    public string? GetValue(string key)
    {
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
    public string GetValue(string key, string defaultValue)
    {
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
    public T? GetValue<T>(string key)
    {
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
    public T GetValue<T>(string key, T defaultValue)
    {
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
    public string? GetConnectionString(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Connection string name cannot be null or empty", nameof(name));

        return _configuration.GetConnectionString(name);
    }

    /// <summary>
    /// Checks if a configuration key exists
    /// </summary>
    /// <param name="key">The configuration key to check</param>
    /// <returns>True if the key exists, false otherwise</returns>
    public bool Exists(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return _configuration[key] != null;
    }

    /// <summary>
    /// Gets a configuration section
    /// </summary>
    /// <param name="key">The section key</param>
    /// <returns>The configuration section</returns>
    public IConfigurationSection GetSection(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Section key cannot be null or empty", nameof(key));

        return _configuration.GetSection(key);
    }
}

