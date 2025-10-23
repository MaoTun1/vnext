using Microsoft.AspNetCore.Http;
using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BBT.Workflow.Telemetry;

/// <summary>
/// OpenTelemetry log processor that enriches log records with vNext-specific metadata.
/// Automatically adds class name, method name, and prefix based on category.
/// </summary>
public sealed class VNextLogEnricherProcessor : BaseProcessor<LogRecord>
{
    private readonly VNextTelemetryOptions _options;
    private readonly List<Regex> _excludedPathPatterns;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the VNextLogEnricherProcessor.
    /// </summary>
    /// <param name="options">The telemetry options</param>
    /// <param name="excludedPathPatterns">Compiled regex patterns for excluded paths</param>
    /// <param name="httpContextAccessor">Optional HTTP context accessor for enriching with request data</param>
    public VNextLogEnricherProcessor(
        VNextTelemetryOptions options, 
        List<Regex> excludedPathPatterns,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _options = options;
        _excludedPathPatterns = excludedPathPatterns;
        _httpContextAccessor = httpContextAccessor;
    }
    /// <summary>
    /// Called when a log record ends. Enriches the record with additional metadata.
    /// </summary>
    /// <param name="record">The log record to enrich</param>
    public override void OnEnd(LogRecord record)
    {
        if (record == null) return;

        // Check if this log should be excluded based on HTTP context path
        if (ShouldExcludeLog())
        {
            return;
        }

        // Add trace/span correlation (already handled by OTel, but we ensure it's present)
        var activity = Activity.Current;
        if (activity != null)
        {
            record.TraceId = activity.TraceId;
            record.SpanId = activity.SpanId;
        }

        // Initialize attributes list if needed
        record.Attributes ??= new List<KeyValuePair<string, object?>>();
        var attrs = record.Attributes.ToList();

        // Add EventId as attributes if present
        if (record.EventId.Id != 0)
        {
            if (attrs.All(a => a.Key != "event.id"))
            {
                attrs.Add(new KeyValuePair<string, object?>("event.id", record.EventId.Id));
            }
            
            if (!string.IsNullOrEmpty(record.EventId.Name) && attrs.All(a => a.Key != "event.name"))
            {
                attrs.Add(new KeyValuePair<string, object?>("event.name", record.EventId.Name));
            }
        }

        // Enrich with class name from category
        if (record.CategoryName is { } category)
        {
            // Extract class name from category (last segment of namespace)
            var className = category.Split('.').LastOrDefault() ?? category;
            
            // Add class if not already present
            if (attrs.All(a => a.Key != "class"))
            {
                attrs.Add(new KeyValuePair<string, object?>("class", className));
            }

            // Add prefix based on category
            if (attrs.All(a => a.Key != "prefix"))
            {
                var prefix = GetPrefixFromCategory(category);
                attrs.Add(new KeyValuePair<string, object?>("prefix", prefix));
            }
        }

        // Add custom attributes from configuration
        foreach (var attr in _options.Logging.Enrichers.CustomAttributes)
        {
            if (attrs.All(a => a.Key != attr.Key))
            {
                attrs.Add(new KeyValuePair<string, object?>(attr.Key, attr.Value));
            }
        }

        // Add HTTP headers from configuration
        EnrichWithHttpHeaders(attrs);

        // Update the record with enriched attributes
        record.Attributes = attrs;
    }

    /// <summary>
    /// Checks if the current log should be excluded based on the HTTP request path.
    /// </summary>
    /// <returns>True if the log should be excluded, false otherwise</returns>
    private bool ShouldExcludeLog()
    {
        if (_httpContextAccessor?.HttpContext == null)
        {
            return false;
        }

        var path = _httpContextAccessor.HttpContext.Request.Path.Value ?? string.Empty;
        
        foreach (var pattern in _excludedPathPatterns)
        {
            if (pattern.IsMatch(path))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Enriches log attributes with configured HTTP headers from the current request.
    /// </summary>
    /// <param name="attrs">The attributes list to enrich</param>
    private void EnrichWithHttpHeaders(List<KeyValuePair<string, object?>> attrs)
    {
        if (_httpContextAccessor?.HttpContext == null)
        {
            return;
        }

        var request = _httpContextAccessor.HttpContext.Request;
        
        foreach (var headerName in _options.Logging.Enrichers.Headers)
        {
            if (request.Headers.TryGetValue(headerName, out var headerValue))
            {
                var key = $"http.header.{headerName.ToLowerInvariant()}";
                if (attrs.All(a => a.Key != key))
                {
                    attrs.Add(new KeyValuePair<string, object?>(key, headerValue.ToString()));
                }
            }
        }
    }

    /// <summary>
    /// Determines the log prefix based on the logger category (namespace).
    /// </summary>
    /// <param name="category">The logger category name</param>
    /// <returns>The appropriate prefix for this category</returns>
    private static string GetPrefixFromCategory(string category)
    {
        return category switch
        {
            var c when c.StartsWith("BBT.Workflow.Execution", StringComparison.OrdinalIgnoreCase) => "vnext.exec",
            var c when c.StartsWith("BBT.Workflow.Orchestration", StringComparison.OrdinalIgnoreCase) => "vnext.orch",
            var c when c.StartsWith("BBT.Workflow.Infrastructure", StringComparison.OrdinalIgnoreCase) => "vnext.infra",
            var c when c.StartsWith("BBT.Workflow.Application", StringComparison.OrdinalIgnoreCase) => "vnext.app",
            var c when c.StartsWith("BBT.Workflow.Domain", StringComparison.OrdinalIgnoreCase) => "vnext.domain",
            _ => "vnext"
        };
    }
}

