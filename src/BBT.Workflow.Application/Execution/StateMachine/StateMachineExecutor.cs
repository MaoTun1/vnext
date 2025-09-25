using System.Text.Json;
using BBT.Aether.Guids;
using BBT.Workflow.BackgroundJobs;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Instances;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using WorkflowExecutionContext = BBT.Workflow.Shared.ExecutionContext;
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
    ITimerExecutionService timerExecutionService,
    IBackgroundJobService backgroundJobService,
    IGuidGenerator guidGenerator,
    IInstanceTransitionRepository instanceTransitionRepository,
    IInstanceRepository instanceRepository,
    ISubFlowService subFlowService,
    IWorkflowMetrics workflowMetrics,
    IScriptContextFactory scriptContextFactory,
    IRuntimeInfoProvider runtimeInfoProvider,
    DaprClient daprClient,
    IConfiguration configuration,
    IAutoTransitionService autoTransitionService,
    ILogger<StateMachineExecutor> logger
) : IStateMachineExecutor
{
    /// <inheritdoc />
    public async Task ExecuteTransitionAsync(
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var instanceTransition = InstanceTransition.Create(
            guidGenerator.Create(),
            context.Instance.Id,
            context.Transition.Key,
            context.Instance.GetCurrentState,
            new JsonData(JsonSerializer.Serialize(context.Body ?? new Dictionary<string, object>())),
            new JsonData(JsonSerializer.Serialize(context.Headers ?? new Dictionary<string, string>())
            )
        );

        await instanceTransitionRepository.InsertAsync(instanceTransition, true, cancellationToken);

        // Record state transition metric
        workflowMetrics.RecordStateTransition(
            context.Workflow.Key,
            context.Instance.GetCurrentState,
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
        if (context.Workflow.GetState(context.Instance.GetCurrentState).OnExits.Any())
        {
            await taskExecutionService.ExecuteAsync(
                context.Workflow.GetState(context.Instance.GetCurrentState).OnExits,
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
            context.Instance.GetCurrentState
        );

        //4. Target State OnEntries
        if (context.Workflow.GetState(context.Instance.GetCurrentState).OnEntries.Any())
        {
            await taskExecutionService.ExecuteAsync(
                context.Workflow.GetState(context.Instance.GetCurrentState).OnEntries,
                instanceTransition,
                TaskTrigger.OnEntry,
                context,
                cancellationToken
            );
        }

        var targetState = context.Workflow.GetState(context.Instance.GetCurrentState);

        // State and data changes are reflected.
        await instanceRepository.UpdateAsync(context.Instance, true, cancellationToken);

        if (targetState.StateType != StateType.Finish)
        {
            //WARNING!: If a state has a subflow, the auto transition process must continue when the subflow is completed. The main flow is already preserving the state.
            //5. Handle SubFlow/SubProcess if target state is SubFlow type
            if (targetState.StateType == StateType.SubFlow)
            {
                await HandleSubFlowAsync(context.Workflow, context.Instance, targetState, context, cancellationToken);

                if (targetState.SubFlow != null && targetState.SubFlow.Type.Equals(SubFlowType.SubProcess))
                {
                    // Check for automatic transitions after state change 
                    await CheckAndExecuteAutomaticTransitionsAsync(
                        context.Workflow,
                        context.Instance,
                        cancellationToken);

                    // Check for delay transition and execution
                    await ScheduleTransitionsForLaterExecutionAsync(
                        context.Workflow,
                        context.Instance,
                        context,
                        cancellationToken);
                }
            }
            else
            {
                // Check for automatic transitions after state change 
                await CheckAndExecuteAutomaticTransitionsAsync(
                    context.Workflow,
                    context.Instance,
                    cancellationToken);

                // Check for delay transition and execution
                await ScheduleTransitionsForLaterExecutionAsync(
                    context.Workflow,
                    context.Instance,
                    context,
                    cancellationToken);
            }
        }

        await InstanceStatusHandleAsync(context.Instance, targetState, context.Workflow, cancellationToken);

        instanceTransition.Completed(context.Instance.GetCurrentState);

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
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var autoTransitions = stateMachineService.GetScheduledTransitions(workflow, instance);
        var transitions = autoTransitions as Transition[] ?? autoTransitions.ToArray();
        if (transitions.Any())
        {
            var tasks = transitions
                .Where(t => t.Timer != null)
                .Select(async transition =>
                {
                    var timerSchedule = await timerExecutionService.ExecuteRuleAsync(
#pragma warning disable CS8604 // Possible null reference argument.
                        transition.Timer, context, cancellationToken);
#pragma warning restore CS8604 // Possible null reference argument.

                    await backgroundJobService.EnqueueTransitionTimerAsync(
                        instance.Id,
                        workflow.Key,
                        workflow.Domain,
                        workflow.Version,
                        transition.Key,
                        timerSchedule,
                        cancellationToken);
                });

            await Task.WhenAll(tasks);
        }
    }

    /// <inheritdoc />
    public async Task CheckAndExecuteAutomaticTransitionsAsync(
        Definitions.Workflow workflow,
        Instance instance,
        CancellationToken cancellationToken = default)
    {
        // Delegate auto transition handling to the dedicated service
        await autoTransitionService.CheckAndExecuteAutomaticTransitionsAsync(
            workflow,
            instance,
            cancellationToken);
    }

    private async Task InstanceStatusHandleAsync(
        Instance instance,
        State targetState,
        Definitions.Workflow workflow,
        CancellationToken cancellationToken = default)
    {
        if (targetState.StateType == StateType.Finish)
        {
            // Complete the instance
            instance.Complete();
            await instanceRepository.UpdateStatusAsync(instance, cancellationToken);
            if (instance is { IsSubItem: true, IsCompleted: true })
            {
                await PublishFlowCompletionEventAsync(instance, workflow, cancellationToken);
            }
        }
    }

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
                CompletedState = instance.GetCurrentState,
                InstanceData = instance.IsSubFlow ? latestData?.Data.JsonElement : null,
                MetaData = instance.MetaData,
                CompletedAt = instance.CompletedAt ?? DateTime.UtcNow,
                Duration = instance.Duration
            };

            // Publish the event using Dapr pub/sub
            await daprClient.PublishEventAsync(
                configuration["DAPR_PUBSUB_STORE_NAME"],
                string.Format(DomainConsts.FlowCompleted, configuration["ASPNETCORE_ENVIRONMENT"]?.ToLower()),
                flowCompletedData,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Successfully published flow completion event for instance {InstanceId}",
                instance.Id);

            // Record the event publication in metrics
            workflowMetrics.RecordDaprPubsubMessagePublished(configuration["DAPR_PUBSUB_STORE_NAME"],
                string.Format(DomainConsts.FlowCompleted, configuration["ASPNETCORE_ENVIRONMENT"]?.ToLower()),
                "success");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to publish flow completion event for instance {InstanceId}",
                instance.Id);

            // Record the failure in metrics
            workflowMetrics.RecordDaprPubsubMessagePublished(configuration["DAPR_PUBSUB_STORE_NAME"],
                string.Format(DomainConsts.FlowCompleted, configuration["ASPNETCORE_ENVIRONMENT"]?.ToLower()),
                "failed");
        }
    }

    public async Task<(Transition validatedTransition, IScriptContextBuilder scriptContextBuilder)>
        ValidateTransitionAsync(
            Definitions.Workflow workflow,
            Instance instance,
            string transitionKey,
            JsonElement? data,
            Dictionary<string, string?>? headers,
            Dictionary<string, string?>? routeValues,
            WorkflowExecutionContext executionContext,
            CancellationToken cancellationToken = default)
    {
        var scriptContextBuilder = scriptContextFactory.NewBuilder()
            .WithWorkflow(workflow)
            .WithInstance(instance)
            .WithRuntime(runtimeInfoProvider)
            .WithBody(data)
            .WithHeaders(headers)
            .WithRouteValues(routeValues)
            .WithTransition(transitionKey);

        var scriptContext = await scriptContextBuilder
            .BuildAsync(cancellationToken);

        var validatedTransition = await stateMachineService.GetTransitionAsync(
            workflow,
            instance,
            transitionKey,
            scriptContext,
            data,
            executionContext,
            cancellationToken
        );

        scriptContextBuilder
            .WithTransition(validatedTransition);

        return (validatedTransition, scriptContextBuilder);
    }
}