using System.Diagnostics;
using OpenTelemetry.Trace;

namespace BBT.Workflow.Telemetry;

/// <summary>
/// Extension methods for Activity to simplify OpenTelemetry operations.
/// </summary>
public static class ActivityExtensions
{
    /// <summary>
    /// Sets the display name for the activity.
    /// </summary>
    public static Activity? SetDisplayName(this Activity? activity, string displayName)
    {
        if (activity != null)
        {
            activity.DisplayName = displayName;
        }
        return activity;
    }

    /// <summary>
    /// Records an exception and sets the activity status to Error.
    /// Wraps OpenTelemetry's RecordException and adds status.
    /// </summary>
    public static Activity? RecordExceptionWithStatus(this Activity? activity, Exception exception, string? description = null)
    {
        if (activity != null)
        {
            activity.RecordException(exception);
            activity.SetStatus(ActivityStatusCode.Error, description ?? exception.Message);
        }
        return activity;
    }
}

