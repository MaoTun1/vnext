namespace BBT.Workflow.Telemetry;

/// <summary>
/// Configuration options for vNext telemetry (logging, tracing, metrics).
/// </summary>
/// <remarks>
/// Service name, version, and OTLP endpoint are read from environment variables:
/// - OTEL_SERVICE_NAME: Service name for telemetry
/// - OTEL_EXPORTER_OTLP_ENDPOINT: OTLP endpoint URL
/// - OTEL_EXPORTER_OTLP_PROTOCOL: OTLP protocol (grpc or http/protobuf)
/// Service version is automatically read from assembly version.
/// </remarks>
public class VNextTelemetryOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Telemetry";

    /// <summary>
    /// Gets or sets the tracing configuration.
    /// </summary>
    public TracingOptions Tracing { get; set; } = new();

    /// <summary>
    /// Gets or sets the logging configuration.
    /// </summary>
    public LoggingOptions Logging { get; set; } = new();
}

/// <summary>
/// Logging configuration options.
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// Gets or sets whether logging is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include formatted message.
    /// </summary>
    public bool IncludeFormattedMessage { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include scopes.
    /// </summary>
    public bool IncludeScopes { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to parse state values.
    /// </summary>
    public bool ParseStateValues { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable console exporter.
    /// </summary>
    public bool EnableConsoleExporter { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable OTLP exporter.
    /// </summary>
    public bool EnableOtlpExporter { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of path patterns to exclude from logging.
    /// Supports regex patterns.
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of headers to enrich logs with.
    /// </summary>
    public EnrichersOptions Enrichers { get; set; } = new();
}

/// <summary>
/// Tracing configuration options.
/// </summary>
public class TracingOptions
{
    /// <summary>
    /// Gets or sets whether to filter from traces.
    /// </summary>
    public bool EnableExcludedPaths { get; set; } = true;
}

/// <summary>
/// Log enrichment configuration.
/// </summary>
public class EnrichersOptions
{
    /// <summary>
    /// Gets or sets the list of HTTP headers to include in logs.
    /// </summary>
    public List<string> Headers { get; set; } = new() { "x-correlation-id", "x-request-id" };

    /// <summary>
    /// Gets or sets custom key-value pairs to include in all logs.
    /// </summary>
    public Dictionary<string, object> CustomAttributes { get; set; } = new();
}

