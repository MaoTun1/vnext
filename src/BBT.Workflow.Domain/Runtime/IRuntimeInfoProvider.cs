using System.Reflection;
using BBT.Aether;
using BBT.Workflow.Domain;
using BBT.Workflow.Domain.Shared;
using BBT.Workflow.ExceptionHandling;

namespace BBT.Workflow.Runtime;

/// <summary>
/// Provides access to runtime information for the workflow system.
/// This interface defines the contract for retrieving runtime version and domain information,
/// as well as validating domain access permissions.
/// </summary>
public interface IRuntimeInfoProvider
{
    /// <summary>
    /// Gets the current version of the workflow runtime system.
    /// </summary>
    /// <value>
    /// A string representing the runtime version, typically in semantic versioning format (e.g., "1.0.0").
    /// </value>
    string Version { get; }

    /// <summary>
    /// Gets the domain name that this runtime instance is configured to serve.
    /// </summary>
    /// <value>
    /// A string representing the domain name that defines the scope of this runtime instance.
    /// </value>
    string Domain { get; }

    /// <summary>
    /// Validates that the requested domain matches the configured runtime domain.
    /// This method ensures that clients can only access workflows within their authorized domain.
    /// </summary>
    /// <param name="requestDomain">The domain name being requested for access.</param>
    /// <exception cref="NotFoundDomainException">
    /// Thrown when the <paramref name="requestDomain"/> does not match the configured runtime domain.
    /// This indicates an unauthorized access attempt to a different domain.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestDomain"/> is null.</exception>
    void Check(string requestDomain);
}

/// <summary>
/// Provides implementation for runtime information access and domain validation.
/// This class retrieves runtime configuration from environment variables and assembly metadata,
/// ensuring secure domain-based access control for the workflow system.
/// </summary>
public class RuntimeInfoProvider : IRuntimeInfoProvider
{
    /// <summary>
    /// Gets the current version of the workflow runtime system.
    /// </summary>
    /// <value>
    /// The runtime version obtained from the APP_VERSION environment variable or assembly metadata.
    /// </value>
    public string Version { get; }

    /// <summary>
    /// Gets the domain name that this runtime instance is configured to serve.
    /// </summary>
    /// <value>
    /// The domain name obtained from the APP_DOMAIN environment variable.
    /// </value>
    public string Domain { get; }

    /// <summary>
    /// Validates that the requested domain matches the configured runtime domain.
    /// This method provides domain-based security by ensuring clients can only access
    /// workflows within their authorized domain scope.
    /// </summary>
    /// <param name="requestDomain">The domain name being requested for access.</param>
    /// <exception cref="NotFoundDomainException">
    /// Thrown when the requested domain does not match the configured runtime domain,
    /// indicating an unauthorized cross-domain access attempt.
    /// </exception>
    public void Check(string requestDomain)
    {
        if (!Domain.Equals(requestDomain))
        {
            throw new NotFoundDomainException(requestDomain, Domain);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeInfoProvider"/> class.
    /// This constructor reads configuration from environment variables and validates that
    /// all required runtime parameters are properly configured.
    /// </summary>
    /// <exception cref="AetherException">
    /// Thrown when required environment variables (APP_VERSION or APP_DOMAIN) are not set
    /// or when they contain invalid values like "unknown".
    /// </exception>
    /// <remarks>
    /// The constructor attempts to read:
    /// - Version from APP_VERSION environment variable, falling back to assembly version information
    /// - Domain from APP_DOMAIN environment variable, falling back to "unknown"
    /// 
    /// Both values must be properly configured (not "unknown") for the runtime to initialize successfully.
    /// </remarks>
    public RuntimeInfoProvider()
    {
        Version = Environment.GetEnvironmentVariable("APP_VERSION") ?? GetAssemblyVersion();
        Domain = Environment.GetEnvironmentVariable("APP_DOMAIN") ?? "unknown";

        if (Version == "unknown" || Domain == "unknown")
        {
            throw new AetherException("APP_VERSION and APP_DOMAIN environment variables must be set.");
        }
    }

    /// <summary>
    /// Retrieves the version information from the executing assembly's metadata.
    /// This method serves as a fallback when the APP_VERSION environment variable is not set.
    /// </summary>
    /// <returns>
    /// The assembly version string obtained from <see cref="AssemblyInformationalVersionAttribute"/>,
    /// assembly version, or "unknown" if no version information is available.
    /// </returns>
    /// <remarks>
    /// The method prioritizes version information in the following order:
    /// 1. AssemblyInformationalVersionAttribute.InformationalVersion
    /// 2. Assembly.GetName().Version
    /// 3. "unknown" as a last resort
    /// </remarks>
    private string GetAssemblyVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return assembly.GetName().Version?.ToString() ?? versionAttribute?.InformationalVersion ?? "unknown";
    }
}