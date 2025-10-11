namespace BBT.Workflow.BackgroundJobs;

/// <summary>
/// Defines constant values for background job names used throughout the workflow system.
/// These constants ensure consistent job naming and facilitate job routing to appropriate handlers.
/// </summary>
public class BackgroundJobConsts
{
    /// <summary>
    /// The job name for workflow timeout jobs that handle workflow instance timeouts.
    /// These jobs are triggered when a workflow instance exceeds its configured timeout duration.
    /// </summary>
    public const string FlowTimeoutJobName = "workflow-timeout-job";
    
    /// <summary>
    /// The job name for transition timer jobs that handle time-based state transitions.
    /// These jobs are scheduled to execute after a specified duration to trigger timed workflow transitions.
    /// </summary>
    public const string TransitionTimerJobName = "workflow-transition-timer-job";
    
    /// <summary>
    /// The job name for asynchronous transition jobs that handle workflow transitions in background.
    /// These jobs are triggered when Sync=true is specified in TransitionInput.
    /// </summary>
    public const string TransitionJobName = "workflow-transition-job";
}