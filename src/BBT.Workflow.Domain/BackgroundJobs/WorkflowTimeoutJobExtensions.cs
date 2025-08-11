using System.Xml;
using Dapr.Jobs.Models;

namespace BBT.Workflow.BackgroundJobs;

/// <summary>
/// Provides extension methods for the IBackgroundJobService interface to simplify
/// the creation and scheduling of workflow-specific background jobs.
/// These methods encapsulate the common patterns for workflow timeout handling,
/// auto-transitions, and timer-based transitions.
/// </summary>
public static class WorkflowTimeoutJobExtensions
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
    /// Enqueues an auto-transition job that will be executed immediately to process
    /// automatic state transitions that don't require user intervention.
    /// </summary>
    /// <param name="jobService">The background job service instance to enqueue the job with.</param>
    /// <param name="instanceId">The unique identifier of the workflow instance.</param>
    /// <param name="flowName">The name of the workflow definition.</param>
    /// <param name="domain">The domain context for the workflow.</param>
    /// <param name="version">The version of the workflow definition.</param>
    /// <param name="currentState">The current state of the workflow instance, or null if not applicable.</param>
    /// <param name="transitionKeys">Array of transition keys that can be automatically processed.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous job enqueue operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public static Task EnqueueAutoTransitionAsync(
        this IBackgroundJobService jobService,
        Guid instanceId,
        string flowName,
        string domain,
        string version,
        string? currentState,
        string[] transitionKeys,
        CancellationToken cancellationToken = default)
    {
        var jobId = $"auto-transition-{flowName}-{instanceId}-{currentState ?? "NA"}";

        var payload = new AutoTransitionPayload
        {
            Domain = domain,
            InstanceId = instanceId,
            FlowName = flowName,
            Version = version,
            TransitionKeys = transitionKeys
        };

        return jobService.EnqueueAsync(
            jobName: BackgroundJobConsts.AutoTransitionJobName,
            jobId: jobId,
            schedule: DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddSeconds(1)), // Execute immediately
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
    /// <param name="timerDuration">The duration to wait before triggering the transition in XML duration format.</param>
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
        string timerDuration,
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
            schedule: DaprJobSchedule.FromDateTime(
                DateTime.UtcNow.Add(
                    XmlConvert.ToTimeSpan(timerDuration)
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
}

/// <summary>
/// Represents the payload data for workflow timeout jobs.
/// This class contains all necessary information to identify and process
/// a workflow instance that has exceeded its timeout duration.
/// </summary>
public sealed class WorkflowTimeoutPayload
{
    /// <summary>
    /// Gets or sets the domain context for the workflow instance.
    /// </summary>
    /// <value>A string representing the workflow domain.</value>
    public string Domain { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the workflow instance that has timed out.
    /// </summary>
    /// <value>A Guid representing the workflow instance ID.</value>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the name of the workflow definition.
    /// </summary>
    /// <value>A string representing the workflow name.</value>
    public string FlowName { get; set; }

    /// <summary>
    /// Gets or sets the version of the workflow definition.
    /// </summary>
    /// <value>A string representing the workflow version.</value>
    public string Version { get; set; }
}

/// <summary>
/// Represents the payload data for auto-transition jobs.
/// This class contains information needed to process automatic workflow transitions
/// that don't require user intervention.
/// </summary>
public sealed class AutoTransitionPayload
{
    /// <summary>
    /// Gets or sets the domain context for the workflow instance.
    /// </summary>
    /// <value>A string representing the workflow domain.</value>
    public string Domain { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the workflow instance.
    /// </summary>
    /// <value>A Guid representing the workflow instance ID.</value>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the name of the workflow definition.
    /// </summary>
    /// <value>A string representing the workflow name.</value>
    public string FlowName { get; set; }

    /// <summary>
    /// Gets or sets the version of the workflow definition.
    /// </summary>
    /// <value>A string representing the workflow version.</value>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the array of transition keys that can be automatically processed.
    /// </summary>
    /// <value>An array of strings representing the available transition keys.</value>
    public string[] TransitionKeys { get; set; }
}

/// <summary>
/// Represents the payload data for transition timer jobs.
/// This class contains information needed to execute a specific workflow transition
/// after a configured time delay.
/// </summary>
public sealed class TransitionTimerPayload
{
    /// <summary>
    /// Gets or sets the domain context for the workflow instance.
    /// </summary>
    /// <value>A string representing the workflow domain.</value>
    public string Domain { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the workflow instance.
    /// </summary>
    /// <value>A Guid representing the workflow instance ID.</value>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the name of the workflow definition.
    /// </summary>
    /// <value>A string representing the workflow name.</value>
    public string FlowName { get; set; }

    /// <summary>
    /// Gets or sets the version of the workflow definition.
    /// </summary>
    /// <value>A string representing the workflow version.</value>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the key identifying the specific transition to execute.
    /// </summary>
    /// <value>A string representing the transition key.</value>
    public string TransitionKey { get; set; }
}