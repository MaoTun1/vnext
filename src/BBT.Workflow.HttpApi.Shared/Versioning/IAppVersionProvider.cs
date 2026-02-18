namespace BBT.Workflow.Versioning;

/// <summary>
/// Provides the current application version read from environment configuration.
/// The version is typically set via the APP_VERSION environment variable at container/build time.
/// </summary>
public interface IAppVersionProvider
{
    /// <summary>
    /// Returns the current application version string.
    /// </summary>
    string GetVersion();
}
