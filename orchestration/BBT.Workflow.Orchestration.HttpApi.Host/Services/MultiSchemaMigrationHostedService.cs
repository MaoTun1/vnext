using BBT.Aether.MultiSchema;
using BBT.Workflow.Data;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Orchestration.Services;

/// <summary>
/// Background service that automatically migrates database schemas on application startup.
/// Handles both system schemas (from RuntimeOptions) and domain schemas (from sys_flows).
/// </summary>
/// <remarks>
/// This service executes once on application startup and performs the following operations:
/// 1. Migrates all system schemas defined in RuntimeOptions (sys-flows, sys-functions, etc.)
/// 2. Queries the sys_flows schema to discover domain schemas from Instance.Key values
/// 3. Migrates discovered domain schemas in parallel with bounded concurrency
/// 4. Uses distributed locking to ensure only one pod migrates each schema
/// </remarks>
internal sealed class MultiSchemaMigrationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<MultiSchemaMigrationHostedService> logger) : BackgroundService
{
    private const int MaxConcurrentMigrations = 3;

    /// <summary>
    /// Executes the schema migration process on application startup.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token for stopping the service.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Multi-schema migration service started");

        try
        {
            // Phase 1: Migrate system schemas from RuntimeOptions
            await MigrateSystemSchemasAsync(stoppingToken);

            // Phase 2: Get and migrate domain schemas from sys_flows
            await MigrateDomainSchemasAsync(stoppingToken);

            logger.LogInformation("Multi-schema migration service completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical error during multi-schema migration");
            throw;
        }
    }

    /// <summary>
    /// Migrates all system schemas defined in RuntimeOptions.
    /// These are core workflow system schemas like sys-flows, sys-functions, etc.
    /// </summary>
    private async Task MigrateSystemSchemasAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var runtimeOptions = scope.ServiceProvider.GetRequiredService<IOptions<RuntimeOptions>>();
        
        if (!runtimeOptions.Value.EnableSchemaMigration)
        {
            logger.LogInformation("Schema migration is disabled. Skipping system schema migrations");
            return;
        }

        var systemSchemas = runtimeOptions.Value.Schemas.Values.ToList();

        if (!systemSchemas.Any())
        {
            logger.LogWarning("No system schemas found in RuntimeOptions");
            return;
        }

        logger.LogInformation("Starting migration of {Count} system schemas", systemSchemas.Count);

        // Migrate system schemas sequentially to ensure proper order
        foreach (var schemaInfo in systemSchemas)
        {
            await MigrateSingleSchemaAsync(schemaInfo.Schema, cancellationToken);
        }

        logger.LogInformation("Completed migration of system schemas");
    }

    /// <summary>
    /// Discovers and migrates domain schemas from the sys_flows schema.
    /// Queries Instance.Key values to identify domain-specific schemas.
    /// </summary>
    private async Task MigrateDomainSchemasAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var runtimeOptions = scope.ServiceProvider.GetRequiredService<IOptions<RuntimeOptions>>();

        if (!runtimeOptions.Value.EnableSchemaMigration)
        {
            logger.LogInformation("Schema migration is disabled. Skipping domain schema migrations");
            return;
        }

        var currentSchema = scope.ServiceProvider.GetRequiredService<ICurrentSchema>();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        try
        {
            // Switch to sys_flows schema to query Instance.Key values
            using (currentSchema.Use(RuntimeSysSchemaInfo.Flows))
            {
                List<string> domainSchemas;
                
                try
                {
                    // Get distinct Instance.Key values (these represent domain schemas)
                    domainSchemas = await dbContext.Instances
                        .Where(i => i.Key != null)
                        .Select(i => i.Key!)
                        .Distinct()
                        .ToListAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to query domain schemas from sys_flows. Schema may not exist yet");
                    return;
                }

                if (!domainSchemas.Any())
                {
                    logger.LogInformation("No domain schemas found in sys_flows");
                    return;
                }

                // Filter out schemas that are already in RuntimeOptions (system schemas)
                var systemSchemaNames = runtimeOptions.Value.Schemas.Values
                    .Select(s => s.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var schemasToMigrate = domainSchemas
                    .Where(key => !systemSchemaNames.Contains(key))
                    .ToList();

                if (!schemasToMigrate.Any())
                {
                    logger.LogInformation("All discovered schemas are system schemas. No domain schemas to migrate");
                    return;
                }

                logger.LogInformation(
                    "Found {TotalCount} schemas in sys_flows, {MigrateCount} domain schemas to migrate",
                    domainSchemas.Count,
                    schemasToMigrate.Count);

                // Migrate domain schemas in parallel with bounded concurrency
                await MigrateSchemasInParallelAsync(schemasToMigrate, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during domain schema migration process");
            // Don't rethrow - allow the service to complete even if domain migration fails
        }
    }

    /// <summary>
    /// Migrates multiple schemas in parallel with bounded concurrency.
    /// Uses a semaphore to limit concurrent migrations.
    /// </summary>
    private async Task MigrateSchemasInParallelAsync(
        List<string> schemas,
        CancellationToken cancellationToken)
    {
        var semaphore = new SemaphoreSlim(MaxConcurrentMigrations);

        var tasks = schemas.Select(schema => Task.Run(async () =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await MigrateSingleSchemaAsync(schema, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }, cancellationToken)).ToArray();

        await Task.WhenAll(tasks);

        logger.LogInformation("Completed parallel migration of {Count} domain schemas", schemas.Count);
    }

    /// <summary>
    /// Migrates a single schema using the schema migration orchestrator.
    /// The orchestrator handles distributed locking and migration execution.
    /// </summary>
    /// <param name="schemaName">The name of the schema to migrate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task MigrateSingleSchemaAsync(
        string schemaName,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var currentSchema = scope.ServiceProvider.GetRequiredService<ICurrentSchema>();
        using (currentSchema.Use(schemaName))
        {
            var orchestrator = scope.ServiceProvider.GetRequiredService<ISchemaMigrationOrchestrator>();

            try
            {
                await orchestrator.MigrateSchemaWithLockAsync(schemaName, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Migration failed for schema {Schema}. Continuing with remaining schemas",
                    schemaName);
                // Don't rethrow - allow other schemas to continue migrating
            }
        }
    }
}

