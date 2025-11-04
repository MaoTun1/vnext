using BBT.Aether.Application.Services;
using BBT.Aether.Guids;
using BBT.Workflow.Caching;
using BBT.Workflow.Domain;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.Headers;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
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
    IGuidGenerator guidGenerator,
    IHeaderService headerService,
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
            Instance? createdInstance = null;
            
            return await LoadWorkflowAsync(input, cancellationToken)
                .OnSuccessAsync(async _ => 
                    await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken))
                .ThenAsync(workflow => PrepareInstanceAsync(workflow, input, cancellationToken))
                .OnSuccess(data => 
                {
                    createdInstance = data.Instance;
                    logger.LogDebug("Created new instance {InstanceId} with key {InstanceKey}", 
                        data.Instance.Id, data.Instance.Key);
                })
                .ThenAsync(data => ExecuteStartTransitionAsync(data, input, cancellationToken))
                .OnFailureAsync(async error => 
                    await HandleValidationFailureAsync(error, createdInstance, cancellationToken))
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
            input.Instance.MetaData,
            input.Sync,
            input.Instance.Callback,
            cancellationToken);

        if (!instanceResult.IsSuccess)
            return Result<(Definitions.Workflow, Instance)>.Fail(instanceResult.Error);

        var instance = instanceResult.Value!;
        
        // Add instance data if provided
        if (input.Instance.Attributes != null)
        {
            var data = new JsonData(input.Instance.Attributes);
            instance.AddData(
                guidGenerator.Create(),
                data,
                workflow.StartTransition.VersionStrategy
            );
        }
        
        // Persist instance
        var saveResult = await ResultExtensions.TryAsync(
            async ct => await instanceRepository.InsertAsync(instance, true, ct),
            cancellationToken,
            ex => Error.Dependency("db.insert", $"Failed to save instance: {ex.Message}"));
        
        return saveResult.IsSuccess 
            ? Result<(Definitions.Workflow, Instance)>.Ok((workflow, instance))
            : Result<(Definitions.Workflow, Instance)>.Fail(saveResult.Error);
    }

    /// <summary>
    /// Step 4: Executes the start transition.
    /// Catches exceptions and converts them to Result pattern for OnFailureAsync handling.
    /// </summary>
    private async Task<Result<StartInstanceOutput>> ExecuteStartTransitionAsync(
        (Definitions.Workflow Workflow, Instance Instance) data,
        StartInstanceInput input,
        CancellationToken cancellationToken)
    {
        var context = input.ToExecutionContext(data.Instance.Id, data.Workflow.StartTransition.Key);
        
        try
        {
            return await workflowExecutionService
                .ExecuteTransitionAsync(context, cancellationToken)
                .MapAsync(transitionOutput => new StartInstanceOutput
                {
                    Id = data.Instance.Id,
                    Status = transitionOutput.Status
                });
        }
        catch (WorkflowValidationException ex)
        {
            // Convert WorkflowValidationException to Result for OnFailureAsync handling
            return Result<StartInstanceOutput>.Fail(ex.Error);
        }
    }
    
    /// <summary>
    /// Handles validation failure by deleting the instance if it's a validation error (schema or policy).
    /// This allows the user to retry the operation without conflicts.
    /// </summary>
    /// <param name="error">The error that occurred during transition execution</param>
    /// <param name="instance">The instance that was created (may be null if creation failed)</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    private async Task HandleValidationFailureAsync(
        Error error,
        Instance? instance,
        CancellationToken cancellationToken)
    {
        // If validation fails (schema or policy), delete the instance to allow retry
        if (instance != null && IsValidationError(error))
        {
            logger.LogWarning(
                "Validation failed for instance {InstanceId} during start transition. Deleting instance to allow retry. Error: {ErrorCode} - {ErrorMessage}",
                instance.Id, error.Code, error.Message);
            
            try
            {
                await instanceRepository.DeleteAsync(instance, true, cancellationToken);
                logger.LogInformation(
                    "Successfully deleted instance {InstanceId} after validation failure",
                    instance.Id);
            }
            catch (Exception deleteEx)
            {
                // Log the deletion error but don't fail - we still want to return the validation error
                logger.LogError(deleteEx,
                    "Failed to delete instance {InstanceId} after validation failure",
                    instance.Id);
            }
        }
    }

    /// <summary>
    /// Determines if an error is a validation error (schema or policy) that requires instance deletion.
    /// </summary>
    private static bool IsValidationError(Error error)
    {
        // Schema validation errors have ValidationErrors collection
        if (error.ValidationErrors is { Count: > 0 })
            return true;
        
        // Policy validation errors (UnauthorizedTransition)
        if (error.Code == WorkflowErrorCodes.UnauthorizedTransition)
            return true;
        
        // Schema validation error code
        if (error.Code == WorkflowErrorCodes.ValidationErrors)
            return true;
        
        // Runtime schema validation error
        if (error.Code == WorkflowErrorCodes.RuntimeSchemaInvalidState)
            return true;
        
        return false;
    }

    /// <summary>
    /// Step 5: Adds workflow header to response.
    /// </summary>
    private StartInstanceOutput AddWorkflowHeader(StartInstanceOutput output, StartInstanceInput input)
    {
        headerService.AddHeader(
            WorkflowInfo.Name,
            WorkflowInfo.Generate(runtimeInfoProvider.Domain, input.Workflow, input.Version ?? "latest", output.Id)
        );
        return output;
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
    /// Executes the transition and returns the output.
    /// </summary>
    private async Task<Result<TransitionOutput>> ExecuteTransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken)
    {
        var context = input.ToExecutionContext(instanceId, transitionKey);
        return await workflowExecutionService.ExecuteTransitionAsync(context, cancellationToken);
    }

    /// <summary>
    /// Adds workflow header to the transition response.
    /// </summary>
    private TransitionOutput AddTransitionHeader(TransitionOutput output, TransitionInput input)
    {
        headerService.AddHeader(
            WorkflowInfo.Name,
            WorkflowInfo.Generate(runtimeInfoProvider.Domain, input.Workflow, input.Version ?? "latest", output.Id)
        );
        return output;
    }


    /// <summary>
    /// Creates and prepares a new instance with the provided parameters.
    /// </summary>
    private async Task<Result<Instance>> CreateAndPrepareInstanceAsync(
        Definitions.Workflow workflow,
        Guid instanceId,
        string instanceKey,
        List<string>? tags,
        ObjectDictionary metadata,
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