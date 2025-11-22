using BBT.Aether.DistributedLock;
using BBT.Aether.Events;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;

namespace BBT.Workflow.Workers.Outbox.Services;

public sealed class MultiSchemaOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IDistributedLockService lockService,
    ILogger<MultiSchemaOutboxProcessor> logger,
    ICurrentSchema currentSchema)
    : IMultiSchemaOutboxProcessor
{
    private const int LockExpiryInSeconds = 300; // 5 minutes

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all workflows to identify schemas
            var schemas = await GetAllSchemasAsync(cancellationToken);

            if (!schemas.Any())
            {
                return;
            }

            logger.LogInformation("Processing outbox for {SchemaCount} schemas", schemas.Count);

            // Process each schema with distributed locking
            foreach (var schema in schemas)
            {
                await ProcessSchemaAsync(schema, cancellationToken);
            }

            logger.LogInformation("Completed outbox processing for all schemas");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while processing outbox messages across all schemas");
            throw;
        }
    }
    
    private async Task<List<string>> GetAllSchemasAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var runtimeService = scope.ServiceProvider.GetRequiredService<IRuntimeService>();

        // Get all workflows from the system schema
        var flows = await runtimeService.GetAsync<Definitions.Workflow>(
            RuntimeSysSchemaInfo.Flows,
            cancellationToken);

        // Extract unique schema names from workflow keys
        // In vnext, each workflow's Key property represents its database schema
        var schemas = flows
            .Where(f => f != null && !string.IsNullOrWhiteSpace(f.Key))
            .Select(f => f!.Key)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        return schemas;
    }
    
    private async Task ProcessSchemaAsync(string schema, CancellationToken cancellationToken)
    {
        var lockKey = $"outbox:{schema}";

        try
        {
            // Try to acquire distributed lock for this schema
            var lockOutcome = await lockService.ExecuteWithLockAsync(
                lockKey,
                async () => await ProcessOutboxForSchemaAsync(schema, cancellationToken),
                LockExpiryInSeconds,
                cancellationToken);

            if (!lockOutcome)
            {
                logger.LogDebug(
                    "Outbox lock for schema {Schema} is held by another instance, skipping",
                    schema);
            }
            else
            {
                logger.LogDebug(
                    "Successfully processed outbox for schema {Schema}",
                    schema);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing outbox for schema {Schema}",
                schema);
            // Continue processing other schemas even if one fails
        }
    }
    
    private async Task ProcessOutboxForSchemaAsync(
        string schema,
        CancellationToken cancellationToken)
    {
        // Switch to the target schema context
        using (currentSchema.Change(schema))
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var schemaManager = scope.ServiceProvider.GetRequiredService<ISchemaManager>();
            var hasSchema = await schemaManager.SchemaExistsAsync(currentSchema.Name, cancellationToken);
            if (hasSchema)
            {
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

                logger.LogDebug("Processing outbox messages for schema {Schema}", schema);

                await processor.RunAsync(cancellationToken);
            }
        }
    }
}