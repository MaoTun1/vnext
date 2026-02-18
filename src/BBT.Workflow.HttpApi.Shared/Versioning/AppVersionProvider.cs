namespace BBT.Workflow.Versioning;

/// <summary>
/// Reads the application version from the APP_VERSION environment variable.
/// Falls back to "unknown" when the variable is not set.
/// Registered as singleton since the value does not change at runtime.
/// </summary>
public sealed class AppVersionProvider : IAppVersionProvider
{
    private const string FALLBACK_VERSION = "unknown";
    private readonly string version;

    /// <summary>
    /// Initializes a new instance reading the APP_VERSION environment variable.
    /// </summary>
    public AppVersionProvider()
    {
        version = Environment.GetEnvironmentVariable("APP_VERSION") ?? FALLBACK_VERSION;
    }

    /// <inheritdoc />
    public string GetVersion() => version;
}
