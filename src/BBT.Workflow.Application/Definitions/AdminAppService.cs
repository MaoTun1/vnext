using System.Text.Json;
using BBT.Aether.Application.Services;
using BBT.Aether.MultiSchema;
using BBT.Aether.Results;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions.CastHandlers;
using BBT.Workflow.Definitions.Validators;
using BBT.Workflow.Instances;
using BBT.Workflow.Logging;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nito.Disposables;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Administrative service implementation for workflow management operations.
/// Uses Railway pattern - returns Result types instead of throwing exceptions for domain errors.
/// </summary>
public sealed class AdminAppService(
    ICurrentSchema currentSchema,
    IRuntimeInfoProvider runtimeInfoProvider,
    IOptions<RuntimeOptions> runtimeOptions,
    IInstanceRepository instanceRepository,
    WorkflowValidator workflowValidator,
    IDomainCacheContext domainCacheContext,
    WorkflowCastProcessor castProcessor,
    IWorkflowMetrics workflowMetrics,
    IRuntimeCacheInitializer runtimeCacheInitializer,
    IServiceProvider serviceProvider)
    : ApplicationService(serviceProvider), IAdminAppService
{
    /// <inheritdoc />
    public async Task<Result> PublishAsync(PublishInput input, CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);

        // Validate schema - returns Result.Fail instead of throwing
        var schemaValidation = ValidateSchema(input.Key, input.Flow);
        if (!schemaValidation.IsSuccess)
            return schemaValidation;

        using (currentSchema.Use(input.Flow))
        {
            var instance = await instanceRepository.FindByKeyAsync(input.Key, cancellationToken)
                           ?? Instance.Create(GuidGenerator.Create(), input.Flow, input.Key);

            instance.AddTags(input.Tags.ToArray());

            return instance.IsTransient
                ? await SaveNewInstanceAsync(instance, input, cancellationToken)
                : await HandleExistingInstanceAsync(instance, input, cancellationToken);
        }
    }

    /// <summary>
    /// Validates if the provided schema keys are valid according to runtime options.
    /// Returns Result.Fail instead of throwing exception.
    /// </summary>
    private Result ValidateSchema(params string[] schemaKeys)
    {
        foreach (var key in schemaKeys)
        {
            if (!runtimeOptions.Value.Schemas.ContainsKey(key))
            {
                return Result.Fail(WorkflowErrors.SchemaNotConfigured(key));
            }
        }
        return Result.Ok();
    }

    /// <summary>
    /// Creates and validates a workflow from the publish input.
    /// Returns Result instead of throwing exception for validation failures.
    /// </summary>
    private Result<Workflow> CreateAndValidateWorkflow(PublishInput input)
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

            return Result<Workflow>.Fail(
                Error.Validation(
                    WorkflowErrorCodes.InvalidWorkflow,
                    "Workflow validation failed",
                    validationResult.ValidationErrors));
        }

        return Result<Workflow>.Ok(workflow);
    }

    /// <summary>
    /// Saves a new workflow instance to the repository.
    /// </summary>
    private async Task<Result> SaveNewInstanceAsync(
        Instance instance,
        PublishInput input,
        CancellationToken cancellationToken)
    {
        var workflowResult = CreateAndValidateWorkflow(input);
        if (!workflowResult.IsSuccess)
            return workflowResult.ToResult();

        var workflow = workflowResult.Value!;

        instance.AddDataWithVersion(
            GuidGenerator.Create(),
            new JsonData(JsonSerializer.Serialize(workflow, JsonSerializerConstants.JsonOptions)),
            input.Version
        );

        await instanceRepository.InsertAsync(instance, true, cancellationToken);
        await domainCacheContext.Workflows.SetAsync(workflow, cancellationToken);

        if (input.Data?.Any() == true)
        {
            await HandleAdditionalDataVersionsAsync(input, cancellationToken);
        }

        return Result.Ok();
    }

    /// <summary>
    /// Handles updating an existing workflow instance.
    /// </summary>
    private async Task<Result> HandleExistingInstanceAsync(
        Instance instance,
        PublishInput input,
        CancellationToken cancellationToken)
    {
        var existingInstanceData = instance.FindData(input.Version);
        if (existingInstanceData != null)
        {
            if (input.Data?.Any() == true)
            {
                Logger.LogWarning(
                    "Instance {InstanceKey} already has data version {Version}",
                    instance.Key, input.Version);
                await HandleAdditionalDataVersionsAsync(input, cancellationToken);
            }

            return Result.Fail(WorkflowErrors.WorkflowVersionConflict());
        }

        var workflowResult = CreateAndValidateWorkflow(input);
        if (!workflowResult.IsSuccess)
            return workflowResult.ToResult();

        var workflow = workflowResult.Value!;

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
    /// Handles processing additional data versions from the publish input.
    /// </summary>
    private async Task HandleAdditionalDataVersionsAsync(
        PublishInput input,
        CancellationToken cancellationToken)
    {
        using (currentSchema.Use(input.Key))
        {
            await using var scopeProvider = ServiceProvider.CreateAsyncScope();
            var instanceRepo = scopeProvider.ServiceProvider.GetRequiredService<IInstanceRepository>();

            foreach (var dataItem in input.Data!)
            {
                await ProcessDataItemAsync(instanceRepo, input, dataItem, cancellationToken);
            }

            scopeProvider.ToAsyncDisposable();
        }
    }

    /// <summary>
    /// Processes a single data item for additional data versions.
    /// </summary>
    private async Task ProcessDataItemAsync(
        IInstanceRepository instanceRepo,
        PublishInput input,
        PublishDataInput dataItem,
        CancellationToken cancellationToken)
    {
        var instance = await instanceRepo.FindByKeyAsync(dataItem.Key, cancellationToken)
                       ?? Instance.Create(GuidGenerator.Create(), input.Key, dataItem.Key);

        if (instance.FindData(dataItem.Version) != null)
        {
            Logger.LogWarning(
                "Instance {InstanceKey} already has data version {Version}",
                dataItem.Key, dataItem.Version);
            return;
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

    /// <inheritdoc />
    public async Task<Result> InvalidateCacheAsync(
        InvalidateCacheInput input,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(input.Domain);

        // Validate schema - returns Result.Fail instead of throwing
        var schemaValidation = ValidateSchema(input.Flow);
        if (!schemaValidation.IsSuccess)
            return schemaValidation;

        using (currentSchema.Use(input.Flow))
        {
            var instance = await instanceRepository.FindByKeyAsync(input.Key, cancellationToken);
            if (instance == null)
            {
                return Result.Fail(WorkflowErrors.InstanceNotFound(input.Key));
            }

            var instanceData = instance.FindData(input.Version);
            if (instanceData == null)
            {
                return Result.Fail(WorkflowErrors.InstanceDataNotFound(input.Key, input.Version));
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
    public async Task<Result> ReInitializeAsync(CancellationToken cancellationToken = default)
    {
        await runtimeCacheInitializer.InitializeAsync(cancellationToken);
        return Result.Ok();
    }
}