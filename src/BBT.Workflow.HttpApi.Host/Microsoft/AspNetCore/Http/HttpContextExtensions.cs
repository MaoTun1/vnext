namespace Microsoft.AspNetCore.Http;

public static class HttpContextExtensions
{
    private static string? GetHeaderValue(HttpContext context, string headerName)
    {
        return context.Request.Headers.TryGetValue(headerName, out var value) ? value.ToString() : null;
    }

    public static DeviceInfo? GetDeviceInfo(this HttpContext context)
    {
        var headerValue = GetHeaderValue(context, DeviceInfo.Name);
        if (string.IsNullOrEmpty(headerValue)) return null;

        var parts = headerValue.Split(',');
        if (parts.Length != 2) return null;

        return new DeviceInfo(
            Guid.TryParse(parts[0], out var deviceId) ? deviceId : null,
            Guid.TryParse(parts[1], out var installationId) ? installationId : null
        );
    }

    public static WorkflowInfo? GetWorkflowInfo(this HttpContext context)
    {
        var headerValue = GetHeaderValue(context, WorkflowInfo.Name);
        if (string.IsNullOrEmpty(headerValue)) return null;

        var parts = headerValue.Split(',');
        if (parts.Length != 4) return null;

        return new WorkflowInfo(
            domain: parts[0],
            workflow: parts[1],
            version: parts[2],
            instanceId: Guid.TryParse(parts[3], out var instanceId) ? instanceId : null
        );
    }

    public static ActorInfo? GetActorInfo(this HttpContext context)
    {
        var headerValue = GetHeaderValue(context, ActorInfo.Name);
        if (string.IsNullOrEmpty(headerValue)) return null;

        var parts = headerValue.Split(',');
        if (parts.Length != 4) return null;

        return new ActorInfo(
            userId: Guid.TryParse(parts[0], out var userId) ? userId : null,
            userReference: parts[1],
            scopeId: Guid.TryParse(parts[2], out var scopeId) ? scopeId : null,
            scopeReference: parts[3]
        );
    }
}