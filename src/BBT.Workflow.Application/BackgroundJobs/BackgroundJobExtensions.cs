using System.Xml;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Definitions.Timer;
using Dapr.Jobs.Models;

namespace BBT.Workflow.BackgroundJobs;

/// <summary>
/// Provides extension methods for the IBackgroundJobService interface to simplify
/// the creation and scheduling of workflow-specific background jobs.
/// These methods encapsulate the common patterns for workflow timeout handling,
/// auto-transitions, and timer-based transitions.
/// </summary>
public static class BackgroundJobExtensions
{
    /// <summary>
    /// Enqueues a workflow timeout job that will be triggered when the specified timeout duration elapses.
    /// This job is responsible for handling workflow instances that exceed their configured timeout.
    /// </summary>
    /// <param name="jobService">The background job service instance to enqueue the job with.</param>
    /// <param name="instanceId">The unique identifier of the workflow instance.</param>
    /// <param name="flowName">The name of the workflow definition.</param>
    /// <param name="domain">The domain context for the workflow.</param>
    /// <param name="version">The version of the workflow definition.</param>
    /// <param name="timeout">The timeout duration in XML duration format (e.g., "PT5M" for 5 minutes).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous job enqueue operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the timeout format is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public static Task EnqueueFlowTimeoutAsync(
        this IBackgroundJobService jobService,
        Guid instanceId,
        string flowName,
        string domain,
        string version,
        string timeout,
        CancellationToken cancellationToken = default)
    {
        var jobId = $"timeout-{flowName}-{instanceId}";

        var payload = new WorkflowTimeoutPayload
        {
            Domain = domain,
            InstanceId = instanceId,
            FlowName = flowName,
            Version = version
        };

        return jobService.EnqueueAsync(
            jobName: BackgroundJobConsts.FlowTimeoutJobName,
            jobId: jobId,
            schedule: DaprJobSchedule.FromDateTime(
                DateTime.UtcNow.Add(
                    XmlConvert.ToTimeSpan(timeout)
                )
            ),
            payload: payload,
            new Dictionary<string, string>()
            {
                { "domain", payload.Domain },
                { "flowName", payload.FlowName },
                { "instanceId", payload.InstanceId.ToString() }
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Enqueues a transition timer job that will be triggered after the specified duration
    /// to execute a time-based workflow transition.
    /// </summary>
    /// <param name="jobService">The background job service instance to enqueue the job with.</param>
    /// <param name="instanceId">The unique identifier of the workflow instance.</param>
    /// <param name="flowName">The name of the workflow definition.</param>
    /// <param name="domain">The domain context for the workflow.</param>
    /// <param name="version">The version of the workflow definition.</param>
    /// <param name="transitionKey">The key identifying the specific transition to execute.</param>
    /// <param name="timer">The duration to wait before triggering the transition in XML duration format.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous job enqueue operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the timer duration format is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public static Task EnqueueTransitionTimerAsync(
        this IBackgroundJobService jobService,
        Guid instanceId,
        string flowName,
        string domain,
        string version,
        string transitionKey,
        TimerSchedule timer,
        CancellationToken cancellationToken = default)
    {
        var jobId = $"timer-transition-{flowName}-{instanceId}-{transitionKey}";

        var payload = new TransitionTimerPayload
        {
            Domain = domain,
            InstanceId = instanceId,
            FlowName = flowName,
            Version = version,
            TransitionKey = transitionKey
        };

        return jobService.EnqueueAsync(
            jobName: BackgroundJobConsts.TransitionTimerJobName,
            jobId: jobId,
            schedule: timer.ToDaprJobSchedule(),
            payload: payload,
            new Dictionary<string, string>()
            {
                { "domain", payload.Domain },
                { "flowName", payload.FlowName },
                { "instanceId", payload.InstanceId.ToString() }
            },
            cancellationToken: cancellationToken);
    }
}

