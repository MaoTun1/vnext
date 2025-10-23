using System.Text.Json;
using BBT.Aether.Application.Services;
using BBT.Aether.Validation;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions.CastHandlers;
using BBT.Workflow.Definitions.Validators;
using BBT.Workflow.Domain;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Instances;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nito.Disposables;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Administrative service implementation for workflow management operations
/// </summary>
/// <param name="serviceProvider">The service provider for dependency injection</param>
/// <param name="schemaManager">The schema manager for database schema operations</param>
/// <param name="currentSchema">The current schema context</param>
/// <param name="runtimeInfoProvider">The runtime information provider</param>
/// <param name="runtimeOptions">The runtime configuration options</param>
/// <param name="instanceRepository">The repository for workflow instances</param>
/// <param name="workflowValidator">The workflow validator</param>
/// <param name="domainCacheContext">The domain cache context</param>
/// <param name="castProcessor">The workflow cast processor</param>
/// <param name="workflowMetrics">The workflow metrics service for recording metrics</param>
public sealed class AdminAppService(
    IServiceProvider serviceProvider,
    ISchemaManager schemaManager,
    ICurrentSchema currentSchema,
    IRuntimeInfoProvider runtimeInfoProvider,
    IOptions<RuntimeOptions> runtimeOptions,
    IInstanceRepository instanceRepository,
    WorkflowValidator workflowValidator,
    IDomainCacheContext domainCacheContext,
    WorkflowCastProcessor castProcessor,
    IWorkflowMetrics workflowMetrics)
    : ApplicationService(serviceProvider), IAdminAppService
{
    /// <inheritdoc />
    public async Task<Result> PublishAsync(PublishInput input, CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);

        CheckValidSchema(input);

        using (currentSchema.Change(input.Flow))
        {
            await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);
            var instance = await instanceRepository.FindByKeyAsync(input.Key, cancellationToken)
                           ?? Instance.Create(
                               GuidGenerator.Create(),
                               input.Flow,
                               input.Key
                           );

            instance.AddTags(input.Tags.ToArray());

            if (instance.IsTransient)
            {
                await SaveNewInstanceAsync(instance, input, cancellationToken);
                return Result.Ok();
            }

            return await HandleExistingInstanceAsync(instance, input, cancellationToken);
        }
    }

    /// <summary>
    /// Validates if the provided schema is valid according to runtime options
    /// </summary>
    /// <param name="input">The publish input to validate</param>
    /// <returns>True if the schema is valid, false otherwise</returns>
    private void CheckValidSchema(PublishInput input)
    {
        if (!(runtimeOptions.Value.Schemas.ContainsKey(input.Key) &&
              runtimeOptions.Value.Schemas.ContainsKey(input.Flow)))
        {
            throw new RuntimeSchemaInvalidException();
        }
    }

    /// <summary>
    /// Creates and validates a workflow from the publish input
    /// </summary>
    /// <param name="input">The publish input containing workflow definition</param>
    /// <returns>A validated workflow instance</returns>
    /// <exception cref="AetherValidationException">Thrown when workflow validation fails</exception>
    private Workflow CreateAndValidateWorkflow(PublishInput input)
    {
        var workflow = WorkflowFactory.CreateWorkflow(input);
        var validationResult = workflowValidator.Validate(workflow);

        if (!validationResult.IsValid)
        {
            // Record validation failure metrics
            foreach (var error in validationResult.ValidationErrors)
            {
                var memberName = error.MemberNames.FirstOrDefault() ?? "unknown";
                workflowMetrics.RecordValidationFailure("workflow_definition", "AdminAppService", memberName);
            }

            throw new AetherValidationException(validationResult.ValidationErrors);
        }

        return workflow;
    }

    /// <summary>
    /// Saves a new workflow instance to the repository
    /// </summary>
    /// <param name="instance">The instance to save</param>
    /// <param name="input">The publish input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task SaveNewInstanceAsync(Instance instance, PublishInput input,
        CancellationToken cancellationToken)
    {
        var workflow = CreateAndValidateWorkflow(input);
        instance.AddDataWithVersion(
            GuidGenerator.Create(),
            new JsonData(
                JsonSerializer.Serialize(workflow, JsonSerializerConstants.JsonOptions)
            ),
            input.Version
        );
        await instanceRepository.InsertAsync(instance, true, cancellationToken);
        await domainCacheContext.Workflows.SetAsync(workflow, cancellationToken);

        if (input.Data?.Any() == true)
        {
            await HandleAdditionalDataVersionsAsync(input, cancellationToken);
        }
    }

    /// <summary>
    /// Handles updating an existing workflow instance
    /// </summary>
    /// <param name="instance">The existing instance to update</param>
    /// <param name="input">The publish input</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure with appropriate error</returns>
    private async Task<Result> HandleExistingInstanceAsync(Instance instance, PublishInput input,
        CancellationToken cancellationToken)
    {
        var existingInstanceData = instance.FindData(input.Version);
        if (existingInstanceData != null)
        {
            if (input.Data?.Any() == true)
            {
                Logger.LogWarning("Instance {InstanceKey} already has data version {Version}", instance.Key,
                    input.Version);
                await HandleAdditionalDataVersionsAsync(input, cancellationToken);
            }

            return Result.Fail(
                Error.Conflict(
                    WorkflowErrorCodes.ConflictWorkflow,
                    "A record with the same version already exists."));
        }

        var workflow = CreateAndValidateWorkflow(input);

        instance.AddDataWithVersion(
            GuidGenerator.Create(),
            new JsonData(JsonSerializer.Serialize(workflow, JsonSerializerConstants.JsonOptions)),
            input.Version
        );

        await instanceRepository.UpdateAsync(instance, true, cancellationToken);
        await domainCacheContext.Workflows.SetAsync(workflow, cancellationToken);

        if (input.Data?.Any() == true)
        {
            await HandleAdditionalDataVersionsAsync(input, cancellationToken);
        }

        return Result.Ok();
    }

    /// <summary>
    /// Handles processing additional data versions from the publish input
    /// </summary>
    /// <param name="input">The publish input containing additional data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task HandleAdditionalDataVersionsAsync(PublishInput input, CancellationToken cancellationToken)
    {
        using (currentSchema.Change(input.Key))
        {
            var scopeProvider = ServiceProvider.CreateAsyncScope();
            var instanceRepo = scopeProvider.ServiceProvider.GetRequiredService<IInstanceRepository>();

            foreach (var dataItem in input.Data!)
            {
                var instance = await instanceRepo.FindByKeyAsync(dataItem.Key, cancellationToken)
                               ?? Instance.Create(
                                   GuidGenerator.Create(),
                                   input.Key,
                                   dataItem.Key
                               );

                if (instance.FindData(dataItem.Version) != null)
                {
                    Logger.LogWarning("Instance {InstanceKey} already has data version {Version}", dataItem.Key,
                        dataItem.Version);
                    continue;
                }

                instance.AddTags(dataItem.Tags.ToArray());
                instance.AddDataWithVersion(
                    GuidGenerator.Create(),
                    new JsonData(dataItem.Attributes),
                    dataItem.Version
                );

                if (instance.IsTransient)
                {
                    await instanceRepo.InsertAsync(instance, true, cancellationToken);
                }
                else
                {
                    await instanceRepo.UpdateAsync(instance, true, cancellationToken);
                }

                await castProcessor.ProcessAsync(
                    input.Key,
                    new Reference(dataItem.Key, input.Domain, input.Key, dataItem.Version),
                    dataItem.Attributes,
                    cancellationToken
                );
            }

            await scopeProvider.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public async Task<Result> InvalidateCacheAsync(InvalidateCacheInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);

        if (!runtimeOptions.Value.Schemas.ContainsKey(input.Flow))
        {
            throw new RuntimeSchemaInvalidException();
        }

        using (currentSchema.Change(input.Flow))
        {
            var instance = await instanceRepository.FindByKeyAsync(input.Key, cancellationToken);
            if (instance == null)
            {
                return Result.Fail(
                    Error.NotFound(
                        WorkflowErrorCodes.NotFoundInitialState,
                        $"Instance with key '{input.Key}' not found"));
            }

            var instanceData = instance.FindData(input.Version);
            if (instanceData == null)
            {
                return Result.Fail(
                    Error.NotFound(
                        WorkflowErrorCodes.NotFoundInstanceData,
                        $"Instance data not found for key {input.Key} and version {input.Version}"));
            }

            if (instanceData.Data.JsonElement.ValueKind != JsonValueKind.Null)
            {
                await castProcessor.ProcessAsync(
                    input.Flow,
                    new Reference(input.Key, input.Domain, input.Flow, input.Version),
                    instanceData.Data.JsonElement,
                    cancellationToken
                );
            }

            return Result.Ok();
        }
    }

    /// <inheritdoc />
    public async Task ReInitializeAsync(CancellationToken cancellationToken = default)
    {
        var flows = await GetComponentAsync<Definitions.Workflow>(RuntimeSysSchemaInfo.Flows, cancellationToken);
        var tasks = await GetComponentAsync<WorkflowTask>(RuntimeSysSchemaInfo.Tasks, cancellationToken);
        var functions = await GetComponentAsync<Function>(RuntimeSysSchemaInfo.Functions, cancellationToken);
        var views = await GetComponentAsync<View>(RuntimeSysSchemaInfo.Views, cancellationToken);
        var schemas = await GetComponentAsync<SchemaDefinition>(RuntimeSysSchemaInfo.Schemas, cancellationToken);
        var extensions = await GetComponentAsync<Extension>(RuntimeSysSchemaInfo.Extensions, cancellationToken);

        var scope = ServiceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DomainCacheContext>();

        var initialData = new Dictionary<Type, object>
        {
            { typeof(Definitions.Workflow), flows },
            { typeof(WorkflowTask), tasks },
            { typeof(SchemaDefinition), schemas },
            { typeof(Function), functions },
            { typeof(View), views },
            { typeof(Extension), extensions }
        };

        await context.InitializeAsync(initialData, cancellationToken);
        scope.ToAsyncDisposable();
    }

    /// <summary>
    /// Retrieves components of the specified type from the runtime system
    /// </summary>
    /// <typeparam name="T">The type of component to retrieve</typeparam>
    /// <param name="name">The name of the component schema</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of components of the specified type</returns>
    private async Task<IEnumerable<T?>> GetComponentAsync<T>(string name, CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter
    {
        var scope = ServiceProvider.CreateAsyncScope();
        var runtimeService = scope.ServiceProvider.GetRequiredService<IRuntimeService>();

        var items = await runtimeService.GetAsync<T>(name, cancellationToken);
        scope.ToAsyncDisposable();
        return items;
    }
}