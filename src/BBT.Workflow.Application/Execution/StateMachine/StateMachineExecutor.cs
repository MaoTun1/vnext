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

namespace BBT.Workflow.Execution.StateMachine;

/// <inheritdoc />
public sealed class StateMachineExecutor(
    ITaskOrchestrationService taskExecutionService,
    IStateMachineService stateMachineService,
    IBackgroundJobService backgroundJobService,
    IGuidGenerator guidGenerator,
    IInstanceTransitionRepository instanceTransitionRepository,
    ISubFlowService subFlowService,
    IWorkflowMetrics workflowMetrics) : IStateMachineExecutor
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
            // Check for delay transition and execution
            await ScheduleTransitionsForLaterExecutionAsync(
                context.Instance,
                context.Workflow,
                cancellationToken);

            // Check for automatic transitions after state change 
            await CheckAndExecuteAutomaticTransitionsAsync(
                context.Workflow,
                context.Instance,
                cancellationToken);

            //5. Handle SubFlow/SubProcess if target state is SubFlow type
            if (targetState.StateType == StateType.SubFlow)
            {
                await HandleSubFlowAsync(context.Instance, targetState, context, cancellationToken);
            }
        }

        await InstanceStatusHandleAsync(context.Instance, targetState, cancellationToken);

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
    /// <param name="instance">The parent workflow instance.</param>
    /// <param name="targetState">The target state containing SubFlow configuration.</param>
    /// <param name="context">The script context containing execution data.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous SubFlow handling operation.</returns>
    private async Task HandleSubFlowAsync(
        Instance instance,
        State targetState,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        await subFlowService.HandleSubFlowAsync(instance, targetState, context, cancellationToken);
    }

    private async Task ScheduleTransitionsForLaterExecutionAsync(
        Instance instance,
        Definitions.Workflow workflow,
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

    private async Task CheckAndExecuteAutomaticTransitionsAsync(
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
        CancellationToken cancellationToken = default)
    {
        if (targetState.StateType == StateType.Finish)
        {
            try
            {
                // Since SubFlow now creates separate instances, this completion handler 
                // only deals with the completion of the current instance
                instance.Complete();

                // Note: SubFlow completion signaling is now handled via the dedicated endpoint
                // The parent workflow will be notified through the SubFlow completion monitoring mechanism
                // rather than through direct instance completion handling
            }
            catch (Exception)
            {
                // Log error but don't fail the main workflow completion
                // TODO: Add proper logging

                // Fallback: complete the instance anyway
                if (instance.Status != InstanceStatus.Completed)
                {
                    instance.Fault();
                }
            }
        }

        /*
            // Enqueue workflow completion as a background job
            _ = backgroundJobService.EnqueueAsync(
                jobName: "workflow-completion-job",
                jobId: $"completion-{instance.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                schedule: Dapr.Jobs.Models.DaprJobSchedule.FromDateTime(DateTime.UtcNow.AddSeconds(1)),
                payload: new WorkflowCompletionPayload
                {
                    InstanceId = instance.Id,
                    Domain = instance.Domain,
                    FlowName = instance.FlowName,
                    Version = instance.Version
                },
                cancellationToken: cancellationToken);
        */
    }
}