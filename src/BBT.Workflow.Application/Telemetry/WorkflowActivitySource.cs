using System.Diagnostics;

namespace BBT.Workflow.Telemetry;

/// <summary>
/// Centralized ActivitySource for workflow distributed tracing.
/// Use this for creating spans throughout the workflow execution lifecycle.
/// </summary>
public static class WorkflowActivitySource
{
    /// <summary>
    /// The ActivitySource instance for workflow operations.
    /// Register this in OpenTelemetry configuration.
    /// </summary>
    public static readonly ActivitySource Instance = new(
        TelemetryConstants.ActivitySourceName,
        TelemetryConstants.ActivitySourceVersion);
}

