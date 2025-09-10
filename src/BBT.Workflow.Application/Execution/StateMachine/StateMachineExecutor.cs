using System.Text.Json;
using BBT.Aether.Guids;
using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Scripting;
using BBT.Workflow.States;
using BBT.Workflow.SubFlow;
using BBT.Workflow.Tasks;
using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.StateMachine;

/// <inheritdoc />
public sealed class StateMachineExecutor(
    ITaskOrchestrationService taskExecutionService,
    IStateMachineService stateMachineService,
    IBackgroundJobService backgroundJobService,
    IGuidGenerator guidGenerator,
    IInstanceTransitionRepository instanceTransitionRepository,
    ISubFlowService subFlowService,
    IWorkflowMetrics workflowMetrics,
    DaprClient daprClient,
    IConfiguration configuration,
    ILogger<StateMachineExecutor> logger) : IStateMachineExecutor
{
    /// <inheritdoc />
    public async Task ExecuteTransitionAsync(
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var instanceTransition = new InstanceTransition(
            guidGenerator.Create(),
            context.Instance.Id,
            context.Transition.Key,
            context.Instance.CurrentState!,
            new JsonData(JsonSerializer.Serialize(context.Body ?? new Dictionary<string, object>())),
            new JsonData(JsonSerializer.Serialize(context.Headers ?? new Dictionary<string, string>())
            )
        );

        await instanceTransitionRepository.InsertAsync(instanceTransition, true, cancellationToken);

        // Record state transition metric
        workflowMetrics.RecordStateTransition(
            context.Workflow.Key,
            context.Instance.CurrentState!,
            context.Transition.Target
        );

        //1. Transition OnExecutions
        await taskExecutionService.ExecuteAsync(
            context.Transition.OnExecutionTasks,
            instanceTransition,
            TaskTrigger.OnExecute,
            context,
            cancellationToken
        );

        //2. Current state OnExits
        if (context.Workflow.GetState(context.Instance.CurrentState!).OnExits.Any())
        {
            await taskExecutionService.ExecuteAsync(
                context.Workflow.GetState(context.Instance.CurrentState!).OnExits,
                instanceTransition,
                TaskTrigger.OnExit,
                context,
                cancellationToken
            );
        }

        //3. Change state
        context.Instance.ChangeState(context.Transition);

        // Record state entry metric
        workflowMetrics.RecordStateEntry(
            context.Workflow.Key,
            context.Instance.CurrentState!
        );

        //4. Target State OnEntries
        if (context.Workflow.GetState(context.Instance.CurrentState!).OnEntries.Any())
        {
            await taskExecutionService.ExecuteAsync(
                context.Workflow.GetState(context.Instance.CurrentState!).OnEntries,
                instanceTransition,
                TaskTrigger.OnEntry,
                context,
                cancellationToken
            );
        }

        var targetState = context.Workflow.GetState(context.Instance.CurrentState!);

        if (targetState.StateType != StateType.Finish)
        {
            //WARNING!: If a state has a subflow, the auto transition process must continue when the subflow is completed. The main flow is already preserving the state.
            //5. Handle SubFlow/SubProcess if target state is SubFlow type
            if (targetState.StateType == StateType.SubFlow)
            {
                await HandleSubFlowAsync(context.Workflow, context.Instance, targetState, context, cancellationToken);
            }
            else
            {
                // Check for delay transition and execution
                await ScheduleTransitionsForLaterExecutionAsync(
                    context.Workflow,
                    context.Instance,
                    cancellationToken);
                
                // Check for automatic transitions after state change 
                await CheckAndExecuteAutomaticTransitionsAsync(
                    context.Workflow,
                    context.Instance,
                    cancellationToken);
            }
        }

        await InstanceStatusHandleAsync(context.Instance, targetState, context.Workflow, cancellationToken);

        instanceTransition.Completed(context.Instance.CurrentState!);

        // Record state duration metric if duration is available
        if (instanceTransition.Duration.HasValue)
        {
            workflowMetrics.RecordStateDuration(
                context.Workflow.Key,
                instanceTransition.FromState,
                instanceTransition.Duration.Value.TotalSeconds
            );
        }

        await instanceTransitionRepository.UpdateAsync(instanceTransition, true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task FlowTimeoutAsync(
        Definitions.Workflow workflow,
        Instance instance,
        CancellationToken cancellationToken = default)
    {
        if (workflow.Timeout != null)
        {
            await backgroundJobService.EnqueueFlowTimeoutAsync(
                instance.Id,
                workflow.Key,
                workflow.Domain,
                workflow.Version,
                workflow.Timeout.Timer.Duration,
                cancellationToken
            );
        }
    }

    /// <summary>
    /// Handles SubFlow and SubProcess execution based on the workflow type configuration.
    /// This method delegates to the SubFlowService for proper correlation and execution management.
    /// </summary>
    /// <param name="workflow">The current workflow.</param>
    /// <param name="instance">The parent workflow instance.</param>
    /// <param name="targetState">The target state containing SubFlow configuration.</param>
    /// <param name="context">The script context containing execution data.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous SubFlow handling operation.</returns>
    private async Task HandleSubFlowAsync(
        Definitions.Workflow workflow,
        Instance instance,
        State targetState,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        await subFlowService.HandleSubFlowAsync(workflow, instance, targetState, context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ScheduleTransitionsForLaterExecutionAsync(
        Definitions.Workflow workflow,
        Instance instance,
        CancellationToken cancellationToken = default)
    {
        var autoTransitions = stateMachineService.GetScheduledTransitions(workflow, instance);
        var transitions = autoTransitions as Transition[] ?? autoTransitions.ToArray();
        if (transitions.Any())
        {
            var tasks = transitions.Select(transition =>
                backgroundJobService.EnqueueTransitionTimerAsync(
                    instance.Id,
                    workflow.Key,
                    workflow.Domain,
                    workflow.Version,
                    transition.Key,
                    transition.Timer!.Duration,
                    cancellationToken));

            await Task.WhenAll(tasks);
        }
    }

    /// <inheritdoc />
    public async Task CheckAndExecuteAutomaticTransitionsAsync(
        Definitions.Workflow workflow,
        Instance instance,
        CancellationToken cancellationToken = default)
    {
        var autoTransitions = stateMachineService.GetAutomaticTransitions(workflow, instance);
        var transitions = autoTransitions as Transition[] ?? autoTransitions.ToArray();
        if (transitions.Any())
        {
            await backgroundJobService.EnqueueAutoTransitionAsync(
                instance.Id,
                workflow.Key,
                workflow.Domain,
                workflow.Version,
                instance.CurrentState,
                transitions.Select(s => s.Key).ToArray(),
                cancellationToken);
        }
    }

    private async Task InstanceStatusHandleAsync(
        Instance instance,
        State targetState,
        Definitions.Workflow workflow,
        CancellationToken cancellationToken = default)
    {
        if (targetState.StateType == StateType.Finish)
        {
            try
            {
                // Complete the instance
                instance.Complete();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during instance completion for instance {InstanceId}", instance.Id);

                // Fallback: complete the instance anyway
                if (!instance.Status.Equals(InstanceStatus.Completed))
                {
                    instance.Fault();
                }
            }

            if (workflow.IsSub && instance.IsCompleted)
            {
                await PublishFlowCompletionEventAsync(instance, workflow, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Publishes a flow completion event via Dapr pub/sub when an instance completes.
    /// This event can be consumed by parent workflows to handle SubFlow completion.
    /// </summary>
    /// <param name="instance">The completed workflow instance</param>
    /// <param name="workflow">The workflow definition</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task PublishFlowCompletionEventAsync(
        Instance instance,
        Definitions.Workflow workflow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Publishing flow completion event for instance {InstanceId} in domain {Domain}, workflow {WorkflowKey}",
                instance.Id, workflow.Domain, workflow.Key);

            // Get the latest instance data
            var latestData = instance.LatestData;

            // Prepare the flow completion data
            var flowCompletedData = new FlowCompletedData
            {
                InstanceId = instance.Id,
                Domain = workflow.Domain, 
                Workflow = workflow.Key,
                Version = workflow.Version,
                CompletedState = instance.CurrentState!,
                InstanceData = latestData?.Data.JsonElement,
                Tags = instance.Tags.ToArray(),
                CompletedAt = instance.CompletedAt ?? DateTime.UtcNow,
                Duration = instance.Duration
            };

            // Publish the event using Dapr pub/sub
            await daprClient.PublishEventAsync(
                configuration["DAPR_PUBSUB_STORE_NAME"],
                string.Format(PubSubConsts.FlowCompleted, configuration["ASPNETCORE_ENVIRONMENT"]?.ToLower()),
                flowCompletedData,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Successfully published flow completion event for instance {InstanceId}",
                instance.Id);

            // Record the event publication in metrics
            workflowMetrics.RecordDaprPubsubMessagePublished(configuration["DAPR_PUBSUB_STORE_NAME"], 
                string.Format(PubSubConsts.FlowCompleted, configuration["ASPNETCORE_ENVIRONMENT"]?.ToLower()),
                "success");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to publish flow completion event for instance {InstanceId}",
                instance.Id);

            // Record the failure in metrics
            workflowMetrics.RecordDaprPubsubMessagePublished(configuration["DAPR_PUBSUB_STORE_NAME"], 
                string.Format(PubSubConsts.FlowCompleted, configuration["ASPNETCORE_ENVIRONMENT"]?.ToLower()),
                "failed");
        }
    }
}