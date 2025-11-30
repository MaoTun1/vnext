using System.Text.RegularExpressions;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Extensions for HttpContext to extract Workflow-specific information from headers
/// </summary>
public static partial class HttpContextExtensions
{
    // Regex patterns for parsing header values
    [GeneratedRegex(@"^([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}),([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})$")]
    private static partial Regex DeviceInfoPattern();

    [GeneratedRegex(@"^([^,]+),([^,]+),([^,]+),([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})$")]
    private static partial Regex WorkflowInfoPattern();

    [GeneratedRegex(@"^([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}),([^,]+),([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}),([^,]+)$")]
    private static partial Regex ActorInfoPattern();

    private static string? GetHeaderValue(HttpContext context, string headerName)
    {
        return context.Request.Headers.TryGetValue(headerName, out var value) ? value.ToString() : null;
    }

    /// <summary>
    /// Extracts device information from HTTP headers
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>Device information if available</returns>
    public static DeviceInfo? GetDeviceInfo(this HttpContext context)
    {
        var headerValue = GetHeaderValue(context, DeviceInfo.Name);
        if (string.IsNullOrEmpty(headerValue)) return null;

        var match = DeviceInfoPattern().Match(headerValue);
        if (!match.Success) return null;

        return new DeviceInfo(
            deviceId: Guid.Parse(match.Groups[1].Value),
            installationId: Guid.Parse(match.Groups[2].Value)
        );
    }

    /// <summary>
    /// Extracts workflow information from HTTP headers
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>Workflow information if available</returns>
    public static WorkflowInfo? GetWorkflowInfo(this HttpContext context)
    {
        var headerValue = GetHeaderValue(context, WorkflowInfo.Name);
        if (string.IsNullOrEmpty(headerValue)) return null;

        var match = WorkflowInfoPattern().Match(headerValue);
        if (!match.Success) return null;

        return new WorkflowInfo(
            domain: match.Groups[1].Value,
            workflow: match.Groups[2].Value,
            version: match.Groups[3].Value,
            instanceId: Guid.Parse(match.Groups[4].Value)
        );
    }

    /// <summary>
    /// Extracts actor information from HTTP headers
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>Actor information if available</returns>
    public static ActorInfo? GetActorInfo(this HttpContext context)
    {
        var headerValue = GetHeaderValue(context, ActorInfo.Name);
        if (string.IsNullOrEmpty(headerValue)) return null;

        var match = ActorInfoPattern().Match(headerValue);
        if (!match.Success) return null;

        return new ActorInfo(
            userId: Guid.Parse(match.Groups[1].Value),
            userReference: match.Groups[2].Value,
            scopeId: Guid.Parse(match.Groups[3].Value),
            scopeReference: match.Groups[4].Value
        );
    }
} 