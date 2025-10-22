using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.AspNetCore.Builder;

public static class WorkflowHealthCheckMapExtensions
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    public static WebApplication MapAppHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health",
            new HealthCheckOptions
            {
                Predicate = _ => true, AllowCachingResponses = false, ResponseWriter = WriteResponse
            });

        app.MapHealthChecks("/ready",
            new HealthCheckOptions
            {
                Predicate = (check) => check.Tags.Contains("ready"), ResponseWriter = WriteResponse
            });

        app.MapHealthChecks("/live",
            new HealthCheckOptions
            {
                Predicate = (check) => check.Tags.Contains("live"), ResponseWriter = WriteResponse
            });

        return app;
    }

    private static Task WriteResponse(
        HttpContext context,
        HealthReport report)
    {
        var json = JsonSerializer.Serialize(
            new
            {
                Status = report.Status.ToString(),
                Duration = report.TotalDuration,
                Info = report.Entries
                    .Select(e =>
                        new
                        {
                            Key = e.Key,
                            Description = e.Value.Description,
                            Duration = e.Value.Duration,
                            Status = Enum.GetName(e.Value.Status),
                            Error = e.Value.Exception?.Message,
                            Tags = e.Value.Tags,
                            Data = e.Value.Data
                        })
                    .ToList()
            },
            JsonSerializerOptions);

        context.Response.ContentType = MediaTypeNames.Application.Json;
        return context.Response.WriteAsync(json);
    }
}