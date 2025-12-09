using System.Reflection;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Represents an embedded script entry with its resource name and source assembly.
/// </summary>
/// <param name="ResourceName">The manifest resource name of the embedded script.</param>
/// <param name="Assembly">The assembly containing the embedded resource.</param>
public sealed record EmbeddedScriptEntry(string ResourceName, Assembly Assembly);

/// <summary>
/// Configuration options for embedded script resources.
/// Maps logical script keys to their corresponding manifest resource names and assemblies.
/// </summary>
/// <remarks>
/// Script mappings are configured during application startup via DI configuration.
/// Each script must have a corresponding embedded resource in the specified assembly.
/// </remarks>
/// <example>
/// <code>
/// services.ConfigureEmbeddedScripts(options =>
/// {
///     options.Add("notification.default", 
///         "BBT.Workflow.Tasks.Scripting.NotificationMapping.csx",
///         typeof(SomeTypeInDomainAssembly).Assembly);
/// });
/// </code>
/// </example>
public sealed class EmbeddedScriptOptions
{
    /// <summary>
    /// Gets the dictionary mapping script keys to embedded script entries.
    /// </summary>
    /// <value>
    /// A dictionary where:
    /// <list type="bullet">
    /// <item><description>Key: The logical script identifier (e.g., "notification.default")</description></item>
    /// <item><description>Value: The <see cref="EmbeddedScriptEntry"/> containing resource name and assembly</description></item>
    /// </list>
    /// Keys are case-insensitive.
    /// </value>
    public Dictionary<string, EmbeddedScriptEntry> Scripts { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a script mapping with the specified key, resource name, and assembly.
    /// </summary>
    /// <param name="key">The logical script identifier.</param>
    /// <param name="resourceName">The manifest resource name of the embedded script.</param>
    /// <param name="assembly">The assembly containing the embedded resource.</param>
    public void Add(string key, string resourceName, Assembly assembly)
    {
        Scripts.TryAdd(key, new EmbeddedScriptEntry(resourceName, assembly));
    }
}

