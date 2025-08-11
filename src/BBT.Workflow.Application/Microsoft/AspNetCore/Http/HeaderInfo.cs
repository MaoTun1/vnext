namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Represents device information extracted from HTTP headers
/// </summary>
/// <param name="deviceId">The unique device identifier</param>
/// <param name="installationId">The unique installation identifier</param>
public sealed class DeviceInfo(Guid? deviceId, Guid? installationId)
{
    /// <summary>
    /// The HTTP header name for device information
    /// </summary>
    public const string Name = "X-Device";
    
    /// <summary>
    /// Gets the unique device identifier
    /// </summary>
    public Guid? DeviceId { get; } = deviceId;
    
    /// <summary>
    /// Gets the unique installation identifier
    /// </summary>
    public Guid? InstallationId { get; } = installationId;

    /// <summary>
    /// Returns a string representation of the device information
    /// </summary>
    public override string ToString()
    {
        return $"{DeviceId},{InstallationId}";
    }
}

/// <summary>
/// Represents workflow information extracted from HTTP headers
/// </summary>
/// <param name="domain">The workflow domain</param>
/// <param name="workflow">The workflow name</param>
/// <param name="version">The workflow version</param>
/// <param name="instanceId">The workflow instance identifier</param>
public sealed class WorkflowInfo(string domain, string workflow, string version, Guid? instanceId)
{
    /// <summary>
    /// The HTTP header name for workflow information
    /// </summary>
    public const string Name = "X-Workflow";
    
    /// <summary>
    /// Gets the workflow domain
    /// </summary>
    public string Domain { get; } = domain;
    
    /// <summary>
    /// Gets the workflow name
    /// </summary>
    public string Workflow { get; } = workflow;
    
    /// <summary>
    /// Gets the workflow version
    /// </summary>
    public string Version { get; } = version;
    
    /// <summary>
    /// Gets the workflow instance identifier
    /// </summary>
    public Guid? InstanceId { get; } = instanceId;

    /// <summary>
    /// Returns a string representation of the workflow information
    /// </summary>
    public override string ToString()
    {
        return $"{Domain},{Workflow},{Version},{InstanceId}";
    }

    /// <summary>
    /// Generates a workflow header value from the specified parameters
    /// </summary>
    /// <param name="domain">The workflow domain</param>
    /// <param name="workflow">The workflow name</param>
    /// <param name="version">The workflow version</param>
    /// <param name="instanceId">The workflow instance identifier</param>
    /// <returns>A formatted string for the workflow header</returns>
    public static string Generate(string domain, string workflow, string version, Guid? instanceId)
    {
        return $"{domain},{workflow},{version},{instanceId}";
    }
}

/// <summary>
/// Represents actor information extracted from HTTP headers
/// </summary>
/// <param name="userId">The unique user identifier</param>
/// <param name="userReference">The user reference</param>
/// <param name="scopeId">The unique scope identifier</param>
/// <param name="scopeReference">The scope reference</param>
public sealed class ActorInfo(Guid? userId, string userReference, Guid? scopeId, string scopeReference)
{
    /// <summary>
    /// The HTTP header name for actor information
    /// </summary>
    public const string Name = "X-Actor";
    
    /// <summary>
    /// Gets the unique user identifier
    /// </summary>
    public Guid? UserId { get; } = userId;
    
    /// <summary>
    /// Gets the user reference
    /// </summary>
    public string UserReference { get; } = userReference;
    
    /// <summary>
    /// Gets the unique scope identifier
    /// </summary>
    public Guid? ScopeId { get; } = scopeId;
    
    /// <summary>
    /// Gets the scope reference
    /// </summary>
    public string ScopeReference { get; } = scopeReference;

    /// <summary>
    /// Returns a string representation of the actor information
    /// </summary>
    public override string ToString()
    {
        return $"{UserId},{UserReference},{ScopeId},{ScopeReference}";
    }
}

/// <summary>
/// Represents ETag header information
/// </summary>
/// <param name="etag">The ETag value</param>
public sealed class ETagHeaderInfo(string etag)
{
    /// <summary>
    /// The HTTP header name for ETag
    /// </summary>
    public const string Name = "ETag";
    
    /// <summary>
    /// Gets the ETag value
    /// </summary>
    public string ETag { get; } = etag;

    /// <summary>
    /// Returns a string representation of the ETag
    /// </summary>
    public override string ToString()
    {
        return ETag;
    }

    /// <summary>
    /// Generates an ETag header value
    /// </summary>
    /// <param name="etag">The ETag value, or null for empty</param>
    /// <returns>The ETag value or empty string if null</returns>
    public static string Generate(string? etag)
    {
        return etag ?? string.Empty;
    }
}

/// <summary>
/// Represents Cache-Control header information
/// </summary>
/// <param name="fromCache">Whether the response is from cache</param>
public sealed class CacheControlInfo(bool fromCache = false)
{
    /// <summary>
    /// The HTTP header name for Cache-Control
    /// </summary>
    public const string Name = "Cache-Control";
    
    /// <summary>
    /// Gets the cache control type
    /// </summary>
    public string Type { get; } = fromCache ? "public" : "no-cache";

    /// <summary>
    /// Returns a string representation of the cache control directive
    /// </summary>
    public override string ToString()
    {
        return $"{Type}";
    }

    /// <summary>
    /// Generates a Cache-Control header value
    /// </summary>
    /// <param name="fromCache">Whether the response is from cache</param>
    /// <returns>The cache control directive</returns>
    public static string Generate(bool fromCache = false)
    {
        return fromCache ? "public" : "no-cache";
    }
} 