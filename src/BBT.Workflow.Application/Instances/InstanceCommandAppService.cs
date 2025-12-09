using BBT.Aether;
using BBT.Aether.Application.Services;
using BBT.Aether.BackgroundJob;
using BBT.Aether.DistributedLock;
using BBT.Aether.Guids;
using BBT.Aether.Results;
using BBT.Workflow.BackgroundJobs.Handlers;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Caching;
using BBT.Workflow.Logging;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Execution.Transitions.Services;
using BBT.Workflow.Execution.Validation;
using BBT.Workflow.Headers;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using Dapr.Jobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Instances;

public sealed class InstanceCommandAppService(
    IServiceProvider serviceProvider,
    IRuntimeInfoProvider runtimeInfoProvider,
    IWorkflowExecutionService workflowExecutionService,
    IComponentCacheStore componentCacheStore,
    IInstanceRepository instanceRepository,
    IInstanceJobRepository instanceJobRepository,
    IBackgroundJobService backgroundJobService,
    IGuidGenerator guidGenerator,
    IHeaderService headerService,
    ITransitionDataMapper transitionDataMapper,
    ITransitionValidationService transitionValidationService,
    IDistributedLockService distributedLockService,
    ISchemaMigrationOrchestrator schemaMigrationOrchestrator,
    ILogger<InstanceCommandAppService> logger)
    : ApplicationService(serviceProvider), IInstanceCommandAppService
{
    /// <inheritdoc />
    public async Task<Result<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);
        // Set schema context once at the beginning
        return await LoadWorkflowAsync(input, cancellationToken)
            .OnSuccessAsync(async _ =>
                await schemaMigrationOrchestrator.MigrateSchemaWithLockAsync(input.Workflow, cancellationToken))
            .ThenAsync(workflow => PrepareInstanceAsync(workflow, input, cancellationToken))
            .ThenAsync(async data =>
            {
                await ScheduleWorkflowTimeoutIfConfiguredAsync(data.Workflow, data.Instance, cancellationToken);
                return Result<(Definitions.Workflow Workflow, Instance Instance)>.Ok(data);
            })
            .ThenAsync(data => ExecuteStartTransitionAsync(data, input, cancellationToken))
            .OnSuccess(output => AddWorkflowHeader(output, input));
    }

    /// <summary>
    /// Step 2: Loads the workflow definition.
    /// </summary>
    private Task<Result<Definitions.Workflow>> LoadWorkflowAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken)
    {
        return componentCacheStore.GetFlowAsync(
            input.Domain, input.Workflow, input.Version, cancellationToken);
    }

    /// <summary>
    /// Step 3: Prepares the instance (create, configure, persist).
    /// Railway chain: Create Instance → Validate → Map Data → Persist
    /// </summary>
    private Task<Result<(Definitions.Workflow Workflow, Instance Instance)>> PrepareInstanceAsync(
        Definitions.Workflow workflow,
        StartInstanceInput input,
        CancellationToken cancellationToken)
    {
        return CreateAndPrepareInstanceAsync(
                workflow,
                input.Instance.Id ?? guidGenerator.Create(),
                input.Instance.Key,
                input.Instance.Tags?.ToList(),
                input.Instance.ExtraProperties,
                input.Sync,
                input.Instance.Callback,
                cancellationToken)
            .ThenAsync(instance => ValidateStartTransitionAsync(workflow, instance, input, cancellationToken))
            .ThenAsync(instance => MapInstanceDataAsync(workflow, instance, input, cancellationToken))
            .ThenAsync(instance => PersistInstanceAsync(workflow, instance, cancellationToken));
    }

    /// <summary>
    /// Validates the start transition for the instance.
    /// Uses MatchAsync to convert non-generic Result to Result&lt;Instance&gt;.
    /// </summary>
    private Task<Result<Instance>> ValidateStartTransitionAsync(
        Definitions.Workflow workflow,
        Instance instance,
        StartInstanceInput input,
        CancellationToken cancellationToken)
    {
        return transitionValidationService.ValidateStartTransitionAsync(
                workflow,
                instance,
                workflow.StartTransition,
                input.Instance.Attributes,
                runtimeInfoProvider,
                input.Headers,
                cancellationToken)
            .MatchAsync(
                onSuccess: () => Result<Instance>.Ok(instance),
                onFailure: error =>
                {
                    logger.StartTransitionValidationFailed(instance.Id, error.Code);
                    return Result<Instance>.Fail(error);
                });
    }

    /// <summary>
    /// Maps and adds instance data if provided.
    /// </summary>
    private async Task<Result<Instance>> MapInstanceDataAsync(
        Definitions.Workflow workflow,
        Instance instance,
        StartInstanceInput input,
        CancellationToken cancellationToken)
    {
        if (input.Instance.Attributes == null)
            return Result<Instance>.Ok(instance);

        return await transitionDataMapper.MapTransitionDataAsync(
                input.Instance.Attributes,
                workflow.StartTransition,
                workflow,
                instance,
                runtimeInfoProvider,
                input.Headers,
                cancellationToken)
            .Tap(mappedData =>
            {
                if (mappedData != null)
                {
                    instance.AddData(
                        guidGenerator.Create(),
                        new JsonData(mappedData),
                        workflow.StartTransition.VersionStrategy);
                }
            })
            .MapAsync(_ => instance);
    }

    /// <summary>
    /// Persists the instance to the repository.
    /// Infrastructure errors (DB connection, etc.) propagate to middleware - not wrapped in Result.
    /// </summary>
    private async Task<Result<(Definitions.Workflow Workflow, Instance Instance)>> PersistInstanceAsync(
        Definitions.Workflow workflow,
        Instance instance,
        CancellationToken cancellationToken)
    {
        await instanceRepository.InsertAsync(instance, true, cancellationToken);
        return Result<(Definitions.Workflow, Instance)>.Ok((workflow, instance));
    }

    /// <summary>
    /// Step 4: Executes the start transition with distributed lock.
    /// Underlying service returns Result - unexpected exceptions propagate to middleware.
    /// </summary>
    private async Task<Result<StartInstanceOutput>> ExecuteStartTransitionAsync(
        (Definitions.Workflow Workflow, Instance Instance) data,
        StartInstanceInput input,
        CancellationToken cancellationToken)
    {
        var context = input.ToExecutionContext(data.Instance.Id, data.Workflow.StartTransition.Key);
        var resourceId = $"instance:{data.Instance.Id}";

        // Execute transition with distributed lock
        var lockOutcome = await distributedLockService.ExecuteWithLockAsync(
            resourceId,
            () => workflowExecutionService
                .ExecuteTransitionAsync(context, cancellationToken)
                .MapAsync(transitionOutput => new StartInstanceOutput
                {
                    Id = data.Instance.Id,
                    Status = transitionOutput.Status
                }),
            InstanceConstants.TransitionLockExpiryInSeconds,
            cancellationToken);

        // Lock acquisition failure is a domain error → Result.Fail
        if (!lockOutcome.Acquired)
        {
            logger.InstanceLockFailed(data.Instance.Id.ToString());
            return Result<StartInstanceOutput>.Fail(
                Error.Conflict(
                    WorkflowErrorCodes.ConflictWorkflow,
                    "Failed to acquire lock for instance",
                    data.Instance.Id.ToString()));
        }

        return lockOutcome.Result;
    }

    /// <summary>
    /// Schedules a workflow timeout job if the workflow has a timeout configuration.
    /// </summary>
    private async Task ScheduleWorkflowTimeoutIfConfiguredAsync(
        Definitions.Workflow workflow,
        Instance instance,
        CancellationToken cancellationToken)
    {
        // Check if workflow has timeout configuration
        if (workflow.Timeout == null)
        {
            return;
        }

        try
        {
            var jobName = $"timeout-{instance.Id}";
            var payload = new WorkflowTimeoutPayload
            {
                JobName = jobName,
                Domain = workflow.Domain,
                InstanceId = instance.Id,
                FlowName = workflow.Key,
                Version = workflow.Version
            };

            // Parse ISO 8601 duration string to TimeSpan
            var timeoutDuration = System.Xml.XmlConvert.ToTimeSpan(workflow.Timeout.Timer.Duration);

            // Calculate timeout schedule - workflow timeout should be evaluated from creation time
            var timeoutDateTime = DateTime.UtcNow.Add(timeoutDuration);
            var schedule = DaprJobSchedule.FromDateTime(timeoutDateTime).ExpressionValue;

            var metadata = new Dictionary<string, object>
            {
                ["domain"] = workflow.Domain,
                ["flowName"] = workflow.Key,
                ["instanceId"] = instance.Id.ToString(),
                ["timeoutAt"] = timeoutDateTime.ToString("O")
            };

            // Enqueue the timeout job
            var jobId = await backgroundJobService.EnqueueAsync(
                FlowTimeoutJobHandler.HandlerName,
                jobName,
                payload,
                schedule,
                metadata,
                cancellationToken);

            // Track the job in the database
            await instanceJobRepository.InsertAsync(
                InstanceJob.Create(
                    jobId,
                    jobName,
                    jobId,
                    workflow.Domain,
                    workflow.Key,
                    instance.Id
                ),
                true,
                cancellationToken);

            logger.WorkflowTimeoutScheduled(instance.Id, workflow.Timeout.Timer.Duration, timeoutDateTime);
        }
        catch (Exception ex)
        {
            logger.WorkflowTimeoutSchedulingFailed(ex, instance.Id);
            // Don't throw - timeout scheduling failure should not prevent workflow start
        }
    }

    /// <summary>
    /// Step 5: Adds workflow header to response.
    /// </summary>
    private void AddWorkflowHeader(StartInstanceOutput output, StartInstanceInput input)
    {
        headerService.AddHeader(
            WorkflowInfo.Name,
            WorkflowInfo.Generate(runtimeInfoProvider.Domain, input.Workflow, input.Version, output.Id)
        );
    }

    /// <inheritdoc />
    public async Task<Result<TransitionOutput>> TransitionAsync(
        string instance,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        // Validate domain first
        runtimeInfoProvider.Check(input.Domain);
        return await ExecuteTransitionAsync(instance, transitionKey, input, cancellationToken)
            .OnSuccess(output => AddTransitionHeader(output, input));
    }

    /// <summary>
    /// Executes the transition with distributed lock and returns the output.
    /// </summary>
    private async Task<Result<TransitionOutput>> ExecuteTransitionAsync(
        string instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken)
    {
        var context = input.ToExecutionContext(instanceId, transitionKey);
        var resourceId = $"instance:{instanceId}";

        // Execute transition with distributed lock
        var lockOutcome = await distributedLockService.ExecuteWithLockAsync(
            resourceId,
            async () => await workflowExecutionService.ExecuteTransitionAsync(context, cancellationToken),
            InstanceConstants.TransitionLockExpiryInSeconds,
            cancellationToken);

        // Validate lock acquisition
        if (!lockOutcome.Acquired)
        {
            logger.InstanceLockFailed(instanceId);
            return Result<TransitionOutput>.Fail(
                Error.Conflict(
                    WorkflowErrorCodes.ConflictWorkflow,
                    "Failed to acquire lock for instance",
                    instanceId));
        }

        return lockOutcome.Result;
    }

    /// <summary>
    /// Adds workflow header to the transition response.
    /// </summary>
    private void AddTransitionHeader(TransitionOutput output, TransitionInput input)
    {
        headerService.AddHeader(
            WorkflowInfo.Name,
            WorkflowInfo.Generate(runtimeInfoProvider.Domain, input.Workflow, input.Version, output.Id)
        );
    }

    /// <summary>
    /// Creates and prepares a new instance with the provided parameters.
    /// Railway chain: Get Initial State → Check Existing → Create Instance → Configure
    /// </summary>
    private async Task<Result<Instance>> CreateAndPrepareInstanceAsync(
        Definitions.Workflow workflow,
        Guid instanceId,
        string? instanceKey,
        List<string>? tags,
        ExtraPropertyDictionary metadata,
        bool isSync,
        string? callback,
        CancellationToken cancellationToken = default)
    {
        // Check for existing active instance first
        var existingInstance = !instanceKey.IsNullOrWhiteSpace()
            ? await instanceRepository.FindByIdentifierAsync(instanceKey, cancellationToken)
            : await instanceRepository.FindByIdentifierAsync(instanceId.ToString(), cancellationToken);
        if (existingInstance is { IsCompleted: false })
            return Result<Instance>.Fail(WorkflowErrors.InstanceAlreadyExists(
                instanceKey.IsNullOrWhiteSpace() ? instanceId.ToString() : instanceKey)
            );

        // Railway chain: Get initial state → Create and configure instance
        return workflow.GetInitialState()
            .Map(initialState =>
            {
                var instance = Instance.Create(instanceId, workflow.Key, instanceKey);
                instance.SetInfoMetadata(isSync, callback, workflow.Type.Code, metadata);
                instance.ChangeState(initialState);

                if (tags?.Any() == true)
                    instance.AddTags(tags.ToArray());

                return instance;
            });
    }
}