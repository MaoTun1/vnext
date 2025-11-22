using BBT.Aether;
using BBT.Aether.Application.Services;
using BBT.Aether.BackgroundJob;
using BBT.Aether.DistributedLock;
using BBT.Aether.Guids;
using BBT.Aether.Results;
using BBT.Aether.Validation;
using BBT.Workflow.BackgroundJobs.Handlers;
using BBT.Workflow.BackgroundJobs.Payloads;
using BBT.Workflow.Caching;
using BBT.Workflow.Domain;
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
    ICurrentSchema currentSchema,
    IComponentCacheStore componentCacheStore,
    ISchemaManager schemaManager,
    IInstanceRepository instanceRepository,
    IInstanceJobRepository instanceJobRepository,
    IBackgroundJobService backgroundJobService,
    IGuidGenerator guidGenerator,
    IHeaderService headerService,
    ITransitionDataMapper transitionDataMapper,
    ITransitionValidationService transitionValidationService,
    IDistributedLockService distributedLockService,
    ILogger<InstanceCommandAppService> logger)
    : ApplicationService(serviceProvider), IInstanceCommandAppService
{
    /// <inheritdoc />
    public async Task<Result<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        // Each step returns Result, errors propagate automatically
        runtimeInfoProvider.Check(input.Domain);
        // Set schema context once at the beginning
        using (currentSchema.Change(input.Workflow))
        {
            return await LoadWorkflowAsync(input, cancellationToken)
                .OnSuccessAsync(async _ => 
                    await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken))
                .ThenAsync(workflow => PrepareInstanceAsync(workflow, input, cancellationToken))
                .ThenAsync(async data => 
                {
                    await ScheduleWorkflowTimeoutIfConfiguredAsync(data.Workflow, data.Instance, cancellationToken);
                    return Result<(Definitions.Workflow Workflow, Instance Instance)>.Ok(data);
                })
                .ThenAsync(data => ExecuteStartTransitionAsync(data, input, cancellationToken))
                .OnSuccess(output => AddWorkflowHeader(output, input));
        }
    }

    /// <summary>
    /// Step 2: Loads the workflow definition.
    /// </summary>
    private async Task<Result<Definitions.Workflow>> LoadWorkflowAsync(
        StartInstanceInput input, 
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(
            async ct => await componentCacheStore.GetFlowAsync(
                input.Domain, input.Workflow, input.Version, ct),
            cancellationToken,
            _ => WorkflowErrors.WorkflowNotFound(input.Workflow, input.Version));
    }

    /// <summary>
    /// Step 3: Prepares the instance (create, configure, persist).
    /// </summary>
    private async Task<Result<(Definitions.Workflow Workflow, Instance Instance)>> PrepareInstanceAsync(
        Definitions.Workflow workflow,
        StartInstanceInput input,
        CancellationToken cancellationToken)
    {
        // Create instance
        var instanceResult = await CreateAndPrepareInstanceAsync(
            workflow,
            input.Instance.Id ?? guidGenerator.Create(),
            input.Instance.Key,
            input.Instance.Tags?.ToList(),
            input.Instance.ExtraProperties,
            input.Sync,
            input.Instance.Callback,
            cancellationToken);

        if (!instanceResult.IsSuccess)
            return Result<(Definitions.Workflow, Instance)>.Fail(instanceResult.Error);

        var instance = instanceResult.Value!;
        
        // Validate start transition before persisting instance
        var validationResult = await transitionValidationService.ValidateStartTransitionAsync(
            workflow,
            instance,
            workflow.StartTransition,
            input.Instance.Attributes,
            runtimeInfoProvider,
            input.Headers,
            cancellationToken);

        if (!validationResult.IsSuccess)
        {
            logger.LogWarning("Start transition validation failed for instance {InstanceId}: {ErrorCode}",
                instance.Id, validationResult.Error.Code);
            return Result<(Definitions.Workflow, Instance)>.Fail(validationResult.Error);
        }
        
        // Add instance data if provided
        if (input.Instance.Attributes != null)
        {
            // Map start transition data using optional mapping script
            var mappedDataResult = await transitionDataMapper.MapTransitionDataAsync(
                input.Instance.Attributes,
                workflow.StartTransition,
                workflow,
                instance,
                runtimeInfoProvider,
                input.Headers,
                cancellationToken);

            if (!mappedDataResult.IsSuccess)
                return Result<(Definitions.Workflow, Instance)>.Fail(mappedDataResult.Error);

            if (mappedDataResult.Value != null)
            {
                instance.AddData(
                    guidGenerator.Create(),
                    new JsonData(mappedDataResult.Value!),
                    workflow.StartTransition.VersionStrategy
                );
            }
        }
        
        // Persist instance (only after successful validation)
        var saveResult = await ResultExtensions.TryAsync(
            async ct => await instanceRepository.InsertAsync(instance, true, ct),
            cancellationToken,
            ex => Error.Dependency(WorkflowErrorCodes.Dependency, $"Failed to save instance: {ex.Message}"));
        
        return saveResult.IsSuccess 
            ? Result<(Definitions.Workflow, Instance)>.Ok((workflow, instance))
            : Result<(Definitions.Workflow, Instance)>.Fail(saveResult.Error);
    }

    /// <summary>
    /// Step 4: Executes the start transition with distributed lock.
    /// Catches exceptions and converts them to Result pattern for OnFailureAsync handling.
    /// </summary>
    private async Task<Result<StartInstanceOutput>> ExecuteStartTransitionAsync(
        (Definitions.Workflow Workflow, Instance Instance) data,
        StartInstanceInput input,
        CancellationToken cancellationToken)
    {
        var context = input.ToExecutionContext(data.Instance.Id, data.Workflow.StartTransition.Key);
        var resourceId = $"instance:{data.Instance.Id}";

        try
        {
            // Execute transition with distributed lock
            var lockOutcome = await distributedLockService.ExecuteWithLockAsync(
                resourceId,
                async () => await workflowExecutionService
                    .ExecuteTransitionAsync(context, cancellationToken)
                    .MapAsync(transitionOutput => new StartInstanceOutput
                    {
                        Id = data.Instance.Id,
                        Status = transitionOutput.Status
                    }),
                InstanceConstants.TransitionLockExpiryInSeconds,
                cancellationToken);

            // Validate lock acquisition
            if (!lockOutcome.Acquired)
            {
                logger.LogWarning("Failed to acquire lock for instance {InstanceId}", data.Instance.Id);
                return Result<StartInstanceOutput>.Fail(
                    Error.Conflict(
                        WorkflowErrorCodes.ConflictWorkflow,
                        "Failed to acquire lock for instance",
                        data.Instance.Id.ToString()));
            }

            return lockOutcome.Result;
        }
        catch (AetherValidationException ex)
        {
            return Result<StartInstanceOutput>.Fail(
                Error.Validation(ErrorCodes.Validation.InvalidFormat, ex.Message, ex.ValidationErrors));
        }
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

            logger.LogInformation(
                "Successfully scheduled workflow timeout job for instance {InstanceId} with timeout duration {Duration}. Job will execute at {TimeoutAt}",
                instance.Id, workflow.Timeout.Timer.Duration, timeoutDateTime);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to schedule workflow timeout job for instance {InstanceId}",
                instance.Id);
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
            WorkflowInfo.Generate(runtimeInfoProvider.Domain, input.Workflow, input.Version ?? "latest", output.Id)
        );
    }

    /// <inheritdoc />
    public async Task<Result<TransitionOutput>> TransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        // Validate domain first
        runtimeInfoProvider.Check(input.Domain);
        return await ExecuteTransitionAsync(instanceId, transitionKey, input, cancellationToken)
            .OnSuccess(output => AddTransitionHeader(output, input));
    }

    /// <summary>
    /// Executes the transition with distributed lock and returns the output.
    /// </summary>
    private async Task<Result<TransitionOutput>> ExecuteTransitionAsync(
        Guid instanceId,
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
            logger.LogWarning("Failed to acquire lock for instance {InstanceId}", instanceId);
            return Result<TransitionOutput>.Fail(
                Error.Conflict(
                    WorkflowErrorCodes.ConflictWorkflow,
                    "Failed to acquire lock for instance",
                    instanceId.ToString()));
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
            WorkflowInfo.Generate(runtimeInfoProvider.Domain, input.Workflow, input.Version ?? "latest", output.Id)
        );
    }


    /// <summary>
    /// Creates and prepares a new instance with the provided parameters.
    /// </summary>
    private async Task<Result<Instance>> CreateAndPrepareInstanceAsync(
        Definitions.Workflow workflow,
        Guid instanceId,
        string instanceKey,
        List<string>? tags,
        ExtraPropertyDictionary metadata,
        bool isSync,
        string? callback,
        CancellationToken cancellationToken = default)
    {
        // 1. Get initial state using Result Pattern
        var initialStateResult = workflow.GetInitialState();
        if (!initialStateResult.IsSuccess)
            return Result<Instance>.Fail(initialStateResult.Error);

        // 2. Check for existing instance
        var existingInstance = await instanceRepository.FindByKeyAsReadOnlyAsync(instanceKey, cancellationToken);

        // 3. If instance exists and is not completed, return conflict error
        if (existingInstance is { IsCompleted: false })
            return Result<Instance>.Fail(WorkflowErrors.InstanceAlreadyExists(instanceKey));

        // 4. Create new instance (existing instance would be completed at this point, so we create new one)
        var instance = Instance.Create(instanceId, workflow.Key, instanceKey);

        // 5. Set system metadata using domain method
        instance.SetInfoMetadata(isSync, callback, workflow.Type.Code, metadata);

        // 6. Initialize instance state and tags (always for new instances)
        instance.ChangeState(initialStateResult.Value!);

        if (tags?.Any() == true)
        {
            instance.AddTags(tags.ToArray());
        }

        return Result<Instance>.Ok(instance);
    }
}