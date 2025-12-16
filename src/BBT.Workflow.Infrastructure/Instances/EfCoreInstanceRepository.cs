using BBT.Aether;
using BBT.Aether.Domain;
using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Aether.Domain.Repositories;
using BBT.Aether.MultiSchema;
using BBT.Aether.Results;
using BBT.Workflow.Data;
using BBT.Workflow.DataSink;
using BBT.Workflow.Definitions;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BBT.Workflow.Instances;

public sealed class EfCoreInstanceRepository(
    IDbContextProvider<WorkflowDbContext> dbContext,
    IServiceProvider serviceProvider,
    IWorkflowMetrics workflowMetrics,
    IRuntimeInfoProvider runtimeInfoProvider,
    IDataSinkManager dataSinkManager,
    ICurrentSchema currentSchema)
    : EfCoreRepository<WorkflowDbContext, Instance, Guid>(dbContext, serviceProvider),
        IInstanceRepository
{
    public override async Task<IQueryable<Instance>> WithDetailsAsync()
    {
        return (await base.WithDetailsAsync())
            .Include(i => i.DataList)
            .Include(i => i.ChildCorrelations);
    }

    /// <summary>
    /// Inserts a new instance and automatically records metrics
    /// </summary>
    public override async Task<Instance> InsertAsync(Instance entity, bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        var result = await base.InsertAsync(entity, autoSave, cancellationToken);

        // Database metrics are automatically recorded by WorkflowDatabaseInterceptor
        // Only record business-specific instance metrics here
        workflowMetrics.RecordInstanceCreated(entity.Flow, runtimeInfoProvider.Domain);

        // Transfer to data sinks (e.g., ClickHouse) if enabled
        try
        {
            await dataSinkManager.HandleInsertAsync(result, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the main operation
            Console.WriteLine($"Failed to transfer instance to data sinks: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Updates an instance and automatically records status change metrics
    /// </summary>
    public override async Task<Instance> UpdateAsync(Instance entity, bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        // Get the original entity to compare status changes
        var originalEntity = await FindAsync(entity.Id, includeDetails: false, cancellationToken);
        var originalStatus = originalEntity?.Status;

        var result = await base.UpdateAsync(entity, autoSave, cancellationToken);

        // Database metrics are automatically recorded by WorkflowDatabaseInterceptor
        // Only handle business-specific status change metrics here
        if (originalStatus != null && !originalStatus.Equals(entity.Status))
        {
            await HandleStatusChangeMetrics(entity, originalStatus, entity.Status);
        }

        // Transfer to data sinks (e.g., ClickHouse) if enabled
        try
        {
            await dataSinkManager.HandleUpdateAsync(result, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the main operation
            Console.WriteLine($"Failed to transfer instance to data sinks: {ex.Message}");
        }

        return result;
    }

    // Transaction metrics are now automatically handled by WorkflowDatabaseInterceptor
    // No need for manual transaction tracking helpers

    public async Task<Instance?> FindByIdentifierAsync(
        string? identifier,
        CancellationToken cancellationToken = default)
    {
        var query = (await WithDetailsAsync())
            .AsSplitQuery();

        if (Guid.TryParse(identifier, out var instanceId))
        {
            return await query
                .FirstOrDefaultAsync(
                    p => p.Id == instanceId,
                    cancellationToken);
        }

        return await query
            .FirstOrDefaultAsync(
                p => p.Key == identifier,
                cancellationToken);
    }

    public async Task<Instance?> FindByIdentifierAsReadOnlyAsync(string identifier,
        CancellationToken cancellationToken = default)
    {
        var query = (await WithDetailsAsync())
            .AsNoTracking()
            .AsSplitQuery();

        if (Guid.TryParse(identifier, out var instanceId))
        {
            return await query
                .FirstOrDefaultAsync(
                    p => p.Id == instanceId,
                    cancellationToken);
        }

        return await query
            .FirstOrDefaultAsync(
                p => p.Key == identifier,
                cancellationToken);
    }

    /// <summary>
    /// Finds active instance data with smart version matching.
    /// </summary>
    /// <param name="key">The instance key to search for</param>
    /// <param name="version">The version to search for. Supports:
    /// <list type="bullet">
    ///     <item><description>Full version (e.g., "1.0.0-pkg.1.17.0+account"): Exact match</description></item>
    ///     <item><description>Artifact version (e.g., "1.0.0" or "1.0.0-alpha.1"): Returns highest pkg version for that artifact</description></item>
    ///     <item><description>Partial version (e.g., "1.0"): Returns highest version matching the prefix</description></item>
    /// </list>
    /// </param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>The matched instance and data model, or null if not found</returns>
    public async Task<InstanceAndDataModel?> FindActiveDataAsync(string key, string version,
        CancellationToken cancellationToken = default)
    {
        var context = await GetDbContextAsync();

        // If full version → exact match (optimized query)
        if (InstanceDataVersionComparer.IsFullVersion(version))
        {
            return await (from instance in context.Instances
                    where instance.Status == InstanceStatus.Active
                    join data in context.InstancesData on instance.Id equals data.InstanceId
                    where instance.Key == key && data.Version == version
                    select new InstanceAndDataModel
                    {
                        Instance = instance,
                        InstanceData = data
                    })
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancellationToken);
        }

        // For artifact or partial version → load all matching versions and use smart matching
        var candidates = await (from instance in context.Instances
                where instance.Status == InstanceStatus.Active && instance.Key == key
                join data in context.InstancesData on instance.Id equals data.InstanceId
                select new InstanceAndDataModel
                {
                    Instance = instance,
                    InstanceData = data
                })
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return null;

        // Use smart version matching
        var versions = candidates.Select(c => c.InstanceData.Version).ToList();
        var bestMatchVersion = InstanceDataVersionComparer.FindBestMatch(versions, version);

        if (string.IsNullOrEmpty(bestMatchVersion))
            return null;

        return candidates.FirstOrDefault(c => 
            string.Equals(c.InstanceData.Version, bestMatchVersion, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IQueryable<Instance>> GetFilteredQueryAsync(
        string[]? filters,
        CancellationToken cancellationToken = default)
    {
        // Apply PostgreSQL native JSON filters if provided
        if (filters?.Any() == true)
        {
            try
            {
                // Use ApplyJsonFilters on Instance DbSet - this matches the working pattern
                // The CTE inside ApplyJsonFilters will handle InstanceData filtering and return Instances
                var filteredInstances = (await GetDbSetAsync())
                    .ApplyJsonFilters(
                        filters: filters,
                        jsonColumnName: "Data", // InstanceData.Data is the JSON column
                        tableName: "InstancesData", // Filter table name
                        schema: currentSchema.Name ?? "public" // Default schema
                    );

                return filteredInstances
                    .Include(i => i.DataList);
            }
            catch (Exception)
            {
                var dbSet = await GetDbSetAsync();
                var query = dbSet.Include(i => i.DataList);
                var filterSpec = new InstanceFilterSpecification(filters);
                return filterSpec.Apply(query);
            }
        }

        // If no filters, use the standard approach with includes
        var standardDbSet = await GetDbSetAsync();
        return standardDbSet.Include(i => i.DataList);
    }

    public async Task<HateoasPagedList<Instance>> GetPagedResultsAsync(
        int page,
        int pageSize,
        string[]? filters,
        CancellationToken cancellationToken = default)
    {
        var query = (await GetFilteredQueryAsync(filters, cancellationToken))
            .Include(i => i.DataList)
            .AsSplitQuery();
        return await query.ToHateoasPagedListAsync(page, pageSize, cancellationToken);
    }

    public async Task<Result<Instance>> GetActiveAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var instanceResult = await GetResultAsync(identifier, includeDetails: true, cancellationToken);

        if (!instanceResult.IsSuccess)
        {
            return instanceResult;
        }

        var instance = instanceResult.Value!;

        if (instance.IsCompleted)
        {
            return Result<Instance>.Fail(Error.Validation(
                WorkflowErrorCodes.InstanceCompleted,
                $"Instance {identifier} is already completed with status: {instance.Status.Code}",
                identifier));
        }

        return Result<Instance>.Ok(instance);
    }

    /// <summary>
    /// Gets an instance by ID using Result pattern.
    /// Returns Result.NotFound if instance doesn't exist.
    /// </summary>
    public async Task<Result<Instance>> GetResultAsync(string identifier, bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        var instance = await FindByIdentifierAsync(identifier, cancellationToken);

        if (instance is null)
        {
            return Result<Instance>.Fail(Error.NotFound(
                WorkflowErrorCodes.InstanceNotFound,
                $"Instance with ID {identifier} not found",
                identifier));
        }

        return Result<Instance>.Ok(instance);
    }

    public async Task<List<InstanceAndDataModel>> GetActiveDataListAsync(CancellationToken cancellationToken = default)
    {
        var context = await GetDbContextAsync();

        // Optimize query with proper indexing and reduced data transfer
        return await (from instance in context.Instances
                where instance.Status == InstanceStatus.Active
                join data in context.InstancesData on instance.Id equals data.InstanceId
                select new InstanceAndDataModel
                {
                    Instance = instance,
                    InstanceData = data
                })
            .AsNoTracking() // Don't track changes for read-only operations
            .AsSplitQuery() // Use split queries for better performance with joins
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AnyActiveByKeyAsync(string key, Guid excludeInstanceId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AsNoTracking()
            .AnyAsync(
                i => i.Key == key 
                     && i.Id != excludeInstanceId 
                     && i.Status == InstanceStatus.Active,
                cancellationToken);
    }

    /// <summary>
    /// Handles metrics recording for instance status changes
    /// </summary>
    private async Task HandleStatusChangeMetrics(Instance entity, InstanceStatus oldStatus, InstanceStatus newStatus)
    {
        // Update status transition metrics (handles all status gauge changes)
        workflowMetrics.UpdateInstanceStatusMetrics(entity.Flow, oldStatus.Code, newStatus.Code);

        // Record specific completion events with duration
        if (newStatus.Equals(InstanceStatus.Completed))
        {
            var durationSeconds = entity.Duration?.TotalSeconds;
            workflowMetrics.RecordInstanceCompleted(entity.Flow, runtimeInfoProvider.Domain, durationSeconds);
        }

        // Record specific error events with duration
        if (newStatus.Equals(InstanceStatus.Faulted))
        {
            var durationSeconds = entity.Duration?.TotalSeconds;
            workflowMetrics.RecordError("instance_faulted", "High", "Instance");

            // Record duration for faulted instances
            if (durationSeconds.HasValue)
            {
                workflowMetrics.RecordInstanceDuration(entity.Flow, "Faulted", durationSeconds.Value);
            }
        }

        await Task.CompletedTask; // For potential future async operations
    }
}
