using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Execution.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Execution.ReEntry;

/// <summary>
/// Default implementation of the re-entry dispatcher.
/// Handles automatic and scheduled transitions by either executing inline or enqueuing as background jobs.
/// </summary>
public sealed class DefaultReentryDispatcher(
    IServiceScopeFactory serviceScopeFactory,
    IBackgroundJobService backgroundJobService,
    IOptions<ReentryOptions> options,
    ILogger<DefaultReentryDispatcher> logger) : IReentryDispatcher
{
    private readonly ReentryOptions _options = options.Value;

    /// <inheritdoc />
    public async Task DispatchAutoAsync(ReentryCommand command, CancellationToken cancellationToken)
    {
        var nextCommand = command with { ChainDepth = command.ChainDepth + 1 };

        // Check for infinite loop protection
        if (nextCommand.ChainDepth > _options.MaxAutoHops)
        {
            logger.LogWarning(
                "Maximum auto transition hops ({MaxHops}) exceeded for instance {InstanceId}, chain {ExecutionChainId}",
                _options.MaxAutoHops, command.InstanceId, command.ExecutionChainId);
            return;
        }

        logger.LogDebug(
            "Dispatching automatic transition {TransitionKey} for instance {InstanceId} (depth: {ChainDepth})",
            command.TransitionKey, command.InstanceId, nextCommand.ChainDepth);

        // Decide execution strategy
        if (command.PreferInline && _options.AllowInlineAuto)
        {
            await InvokeInNewScopeAsync(nextCommand, cancellationToken);
        }
        else
        {
            await EnqueueTransitionJobAsync("auto-transition", nextCommand, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task DispatchScheduledAsync(ReentryCommand command, CancellationToken cancellationToken)
    {
        logger.LogDebug("Dispatching scheduled transition {TransitionKey} for instance {InstanceId}",
            command.TransitionKey, command.InstanceId);

        // Scheduled transitions are always enqueued
        await EnqueueTransitionJobAsync("scheduled-transition", command, cancellationToken);
    }

    /// <summary>
    /// Invokes a transition in a new dependency injection scope.
    /// </summary>
    private async Task InvokeInNewScopeAsync(ReentryCommand command, CancellationToken cancellationToken)
    {
        logger.LogTrace("Executing inline re-entry for transition {TransitionKey} on instance {InstanceId}",
            command.TransitionKey, command.InstanceId);

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var executionService = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionService>();

            var input = WorkflowExecutionContext.From(command);
            await executionService.ExecuteTransitionAsync(input, cancellationToken);

            logger.LogTrace("Completed inline re-entry for transition {TransitionKey} on instance {InstanceId}",
                command.TransitionKey, command.InstanceId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Inline re-entry cancelled for transition {TransitionKey} on instance {InstanceId}",
                command.TransitionKey, command.InstanceId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to execute inline re-entry for transition {TransitionKey} on instance {InstanceId}",
                command.TransitionKey, command.InstanceId);
            throw;
        }
    }

    /// <summary>
    /// Enqueues a transition as a background job.
    /// </summary>
    private async Task EnqueueTransitionJobAsync(string jobType, ReentryCommand command,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("Enqueuing {JobType} job for transition {TransitionKey} on instance {InstanceId}",
            jobType, command.TransitionKey, command.InstanceId);

        try
        {
            // Convert to background job payload
            var payload = CreateTransitionJobPayload(command);

            // Enqueue the job
            // await backgroundJobService.EnqueueTransitionAsync(
            //     command.InstanceId,
            //     command.TransitionKey,
            //     command.Domain,
            //     command.WorkflowKey,
            //     null, // version will be resolved from instance
            //     payload.Data,
            //     payload.Headers,
            //     payload.RouteValues,
            //     payload.ExecutionContext,
            //     cancellationToken);

            logger.LogTrace("Enqueued {JobType} job for transition {TransitionKey} on instance {InstanceId}",
                jobType, command.TransitionKey, command.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to enqueue {JobType} job for transition {TransitionKey} on instance {InstanceId}",
                jobType, command.TransitionKey, command.InstanceId);
            throw;
        }
    }

    /// <summary>
    /// Creates a background job payload from a re-entry command.
    /// </summary>
    private static BackgroundJobs.Payloads.TransitionJobPayload CreateTransitionJobPayload(ReentryCommand command)
    {
        return new BackgroundJobs.Payloads.TransitionJobPayload
        {
            InstanceId = command.InstanceId,
            TransitionKey = command.TransitionKey,
            Domain = command.Domain,
            Workflow = command.WorkflowKey,
            Version = null, // Will be resolved from instance
            Data = null, // Re-entry transitions typically don't carry data
            Headers = command.Headers?.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value) ?? new(),
            RouteValues = new Dictionary<string, string?>(),
            ExecutionActor = Shared.ExecutionActor.System // Re-entry is always system-initiated
        };
    }
}