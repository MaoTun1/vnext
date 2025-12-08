using BBT.Aether.Results;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Infrastructure.Scripting;

/// <summary>
/// Provides access to embedded script resources with lazy loading and caching.
/// Scripts are loaded once from assembly manifest resources and cached in memory.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is designed to be registered as a singleton service.
/// It uses <see cref="Lazy{T}"/> with thread-safe initialization to ensure
/// each script is loaded exactly once, even under concurrent access.
/// </para>
/// <para>
/// Scripts must be configured in <see cref="EmbeddedScriptOptions"/> with their
/// resource names and source assemblies. Each script should be embedded
/// in its assembly using &lt;EmbeddedResource&gt; with LogicalName in the .csproj file.
/// </para>
/// </remarks>
/// <example>
/// Configure in .csproj:
/// <code>
/// &lt;ItemGroup&gt;
///   &lt;EmbeddedResource Include="Scripts\NotificationMapping.csx"
///                     LogicalName="BBT.Workflow.Tasks.Scripting.NotificationMapping.csx" /&gt;
/// &lt;/ItemGroup&gt;
/// </code>
/// Configure in startup:
/// <code>
/// services.ConfigureEmbeddedScripts(opt =>
/// {
///     opt.Add("notification.default", 
///         "BBT.Workflow.Tasks.Scripting.NotificationMapping.csx",
///         typeof(SomeTypeInDomainAssembly).Assembly);
/// });
/// </code>
/// </example>
public sealed class EmbeddedScriptProvider : IEmbeddedScriptProvider
{
    private readonly IReadOnlyDictionary<string, Lazy<string>> _scripts;
    private readonly ILogger<EmbeddedScriptProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedScriptProvider"/> class.
    /// </summary>
    /// <param name="options">Configuration options containing script key to resource mappings.</param>
    /// <param name="logger">Logger for diagnostics and error reporting.</param>
    public EmbeddedScriptProvider(
        IOptions<EmbeddedScriptOptions> options,
        ILogger<EmbeddedScriptProvider> logger)
    {
        _logger = logger;
        _scripts = BuildScriptCache(options.Value, logger);
    }

    /// <inheritdoc />
    public Task<Result<string>> GetAsync(string scriptKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scriptKey))
        {
            return Task.FromResult(
                Result<string>.Fail(
                    Error.Validation("embedded_script.key.empty", "Script key cannot be empty.")));
        }

        if (!_scripts.TryGetValue(scriptKey, out var lazyScript))
        {
            return Task.FromResult(
                Result<string>.Fail(
                    Error.NotFound("embedded_script.key.not_found",
                        $"Embedded script with key '{scriptKey}' is not configured.")));
        }

        try
        {
            // Lazy.Value loads from assembly only on first access, then cached
            var script = lazyScript.Value;

            if (string.IsNullOrWhiteSpace(script))
            {
                _logger.LogWarning("Embedded script for key '{ScriptKey}' is empty", scriptKey);
            }

            return Task.FromResult(Result<string>.Ok(script));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while loading embedded script for key '{ScriptKey}'", scriptKey);

            return Task.FromResult(
                Result<string>.Fail(
                    Error.Failure("embedded_script.load_failed",
                        $"Failed to load embedded script for key '{scriptKey}'.")));
        }
    }

    /// <summary>
    /// Builds the lazy script cache from configuration options.
    /// </summary>
    /// <param name="options">The embedded script options.</param>
    /// <param name="logger">Logger for configuration warnings.</param>
    /// <returns>A read-only dictionary of lazy-loaded scripts.</returns>
    private static IReadOnlyDictionary<string, Lazy<string>> BuildScriptCache(
        EmbeddedScriptOptions options,
        ILogger logger)
    {
        var dict = new Dictionary<string, Lazy<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, entry) in options.Scripts)
        {
            if (string.IsNullOrWhiteSpace(entry.ResourceName))
            {
                logger.LogWarning("Embedded script '{Key}' has empty resource name", key);
                continue;
            }

            var capturedEntry = entry;
            var capturedKey = key;

            dict[key] = new Lazy<string>(() =>
            {
                using var stream = capturedEntry.Assembly.GetManifestResourceStream(capturedEntry.ResourceName)
                    ?? throw new InvalidOperationException(
                        $"Embedded script resource '{capturedEntry.ResourceName}' not found in assembly " +
                        $"'{capturedEntry.Assembly.GetName().Name}' for key '{capturedKey}'. " +
                        "Check <EmbeddedResource LogicalName=...> configuration in .csproj.");

                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        return dict;
    }
}

