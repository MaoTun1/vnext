using BBT.Aether.Application.Services;
using BBT.Aether.Guids;
using BBT.Workflow.Caching;
using BBT.Workflow.Execution.Services;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
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
    ILogger<InstanceCommandAppService> logger)
    : ApplicationService(serviceProvider), IInstanceCommandAppService
{
    /// <inheritdoc />
    public async Task<InstanceServiceResponse<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        // Validate runtime and prepare schema
        runtimeInfoProvider.Check(input.Domain);

        using (currentSchema.Change(input.Workflow))
        {
            // Load workflow and ensure schema
            var workflow = await componentCacheStore.GetFlowAsync(
                input.Domain, input.Workflow, input.Version, cancellationToken);
            
            await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);

            // Create and prepare instance
            var instance = await CreateAndPrepareInstanceAsync(
                workflow,
                input.Instance.Id ?? guidGenerator.Create(),
                input.Instance.Key,
                input.Instance.Tags?.ToList(),
                input.Instance.MetaData,
                input.Sync,
                input.Instance.Callback,
                cancellationToken);

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

            // Persist new instance
            await instanceRepository.InsertAsync(instance, true, cancellationToken);
            logger.LogDebug("Created new instance {InstanceId} with key {InstanceKey}",
                instance.Id, instance.Key);

            // Create WorkflowExecutionContext for start transition
            var context = input.ToExecutionContext(instance.Id, workflow.StartTransition.Key);

            // Execute start transition using WorkflowExecutionService
            var transitionResult = await workflowExecutionService.ExecuteTransitionAsync(context, cancellationToken);

            // Create and return StartInstanceOutput response
            return new InstanceServiceResponse<StartInstanceOutput>(new StartInstanceOutput
            {
                Id = instance.Id,
                Status = transitionResult.Data.Status
            });
        }
    }

    /// <inheritdoc />
    public async Task<InstanceServiceResponse<TransitionOutput>> TransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        // Validate runtime and set schema context
        runtimeInfoProvider.Check(input.Domain);
        
        // Convert TransitionInput to WorkflowExecutionContext
        var context = input.ToExecutionContext(instanceId, transitionKey);
        
        return await workflowExecutionService.ExecuteTransitionAsync(context, cancellationToken);
    }


    /// <summary>
    /// Creates and prepares a new instance with the provided parameters.
    /// </summary>
    private async Task<Instance> CreateAndPrepareInstanceAsync(
        Definitions.Workflow workflow,
        Guid instanceId,
        string instanceKey,
        List<string>? tags,
        ObjectDictionary metadata,
        bool isSync,
        string? callback,
        CancellationToken cancellationToken = default)
    {
        var initialState = workflow.GetInitialState();

        // Check for existing instance
        var existingInstance = await instanceRepository.FindByKeyAsReadOnlyAsync(instanceKey, cancellationToken);

        // If instance exists and is not completed, throw conflict exception
        if (existingInstance is { IsCompleted: false })
        {
            throw new ConflictException();
        }

        // Create new instance (existing instance would be completed at this point, so we create new one)
        var instance = Instance.Create(instanceId, workflow.Key, instanceKey);

        // Set system metadata using domain method
        instance.SetInfoMetadata(isSync, callback, workflow.Type.Code, metadata);

        // Initialize instance state and tags (always for new instances)
        instance.ChangeState(initialState);

        if (tags?.Any() == true)
        {
            instance.AddTags(tags.ToArray());
        }

        return instance;
    }
}