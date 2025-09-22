using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Aether.Domain.Services;
using BBT.Workflow.Data;
using BBT.Workflow.DataSink;
using BBT.Workflow.Definitions;
using BBT.Workflow.Monitoring;
using BBT.Workflow.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace BBT.Workflow.Instances;

public sealed class EfCoreInstanceRepository(
    WorkflowDbContext dbContext,
    IServiceProvider serviceProvider,
    ITransactionService transactionService,
    IConfiguration configuration,
    IWorkflowMetrics workflowMetrics,
    IRuntimeInfoProvider runtimeInfoProvider,
    IDataSinkManager dataSinkManager)
    : EfCoreRepository<WorkflowDbContext, Instance, Guid>(dbContext, serviceProvider, transactionService),
        IInstanceRepository
{
    private readonly WorkflowDbContext _dbContext = dbContext;

    public override async Task<IQueryable<Instance>> WithDetailsAsync()
    {
        return (await base.WithDetailsAsync())
            .Include(i => i.DataList);
    }

    /// <summary>
    /// Inserts a new instance and automatically records metrics
    /// </summary>
    public override async Task<Instance> InsertAsync(Instance entity, bool autoSave = false, CancellationToken cancellationToken = default)
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
    public override async Task<Instance> UpdateAsync(Instance entity, bool autoSave = false, CancellationToken cancellationToken = default)
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

    public async Task<Instance?> FindByKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Include(i => i.DataList)
            .FirstOrDefaultAsync(
            p => p.Key == key, cancellationToken);
    }

    public async Task<Instance?> FindByKeyAsReadOnlyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Include(i => i.DataList)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Key == key, cancellationToken);
    }

    public async Task<Instance?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Include(i => i.DataList)
            .FirstOrDefaultAsync(
            p => p.Id == id, cancellationToken);
    }


    public async Task<InstanceAndDataModel?> FindActiveDataAsync(string key, string version,
        CancellationToken cancellationToken = default)
    {
        var context = await GetDbContextAsync();

        // Optimize query with proper indexing and reduced data transfer
        return await (from instance in context.Instances
                where instance.Status == InstanceStatus.Active
                join data in context.InstancesData on instance.Id equals data.InstanceId
                where instance.Key == key && data.Version == version
                select new InstanceAndDataModel
                {
                    Instance = instance,
                    InstanceData = data
                })
            .AsNoTracking() // Don't track changes for read-only operations
            .AsSplitQuery() // Use split queries for better performance with joins
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IQueryable<Instance>> GetFilteredQueryAsync(
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
                var filteredInstances =  (await GetDbSetAsync())
                    .ApplyJsonFilters(
                        filters: filters,
                        jsonColumnName: "Data", // InstanceData.Data is the JSON column
                        tableName: "InstancesData", // Filter table name
                        schema:  _dbContext.SchemaName ?? "public" // Default schema
                    );

                return filteredInstances
                .Include(i => i.DataList)
                ;
            }
            catch (Exception ex)
            {
                // Fallback to original implementation if PostgreSQL filter fails
                Console.WriteLine($"PostgreSQL filter failed, falling back to EF Core filters: {ex.Message}");
                
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

    /// <summary>
    /// Alternative method that handles DataList loading separately for better performance
    /// with PostgreSQL native filters on InstanceData table
    /// </summary>
    public async Task<IQueryable<Instance>> GetFilteredQueryWithPostgreSqlAsync(
        string[]? filters,
        bool includeDataList = true,
        bool onlyLatest = true,
        CancellationToken cancellationToken = default)
    {
        var context = await GetDbContextAsync();
        
        if (filters?.Any() == true)
        {
            // Apply PostgreSQL native JSON filters on Instance DbSet
            // CTE inside ApplyJsonFilters handles InstanceData filtering and returns Instances
            var filteredInstances = context.Set<Instance>()
                .ApplyJsonFilters(
                    filters: filters,
                    jsonColumnName: "Data", // InstanceData.Data contains the JSON
                    tableName: "InstancesData"
                );

            // Handle DataList inclusion
            if (includeDataList)
            {
                return filteredInstances.Include(i => i.DataList);
            }
            
            return filteredInstances;
        }
        
        // No filters - standard approach
        var query = context.Instances.AsQueryable();
        if (includeDataList)
        {
            query = query.Include(i => i.DataList);
        }
        
        return query;
    }

    /// <summary>
    /// Get filtered InstanceData directly using basic EF Core filters
    /// For now, this method uses standard EF Core filtering instead of PostgreSQL native filters
    /// TODO: Implement InstanceData-specific PostgreSQL native filtering if needed
    /// </summary>
    public async Task<IQueryable<InstanceData>> GetFilteredInstanceDataAsync(
        string[]? filters,
        bool onlyLatest = true,
        CancellationToken cancellationToken = default)
    {
        var context = await GetDbContextAsync();
        var query = context.Set<InstanceData>().AsQueryable();
        
        if (onlyLatest)
        {
            query = query.Where(d => d.IsLatest == true);
        }

        return query;
    }

    public async Task<Definitions.PaginationResult<Instance>> GetPagedResultsAsync(
        int page,
        int pageSize,
        string route,
        IQueryCollection? queryParams = null,
        IQueryable<Instance>? instance = null,
        CancellationToken cancellationToken = default)
    {
        var query = instance ?? (await base.GetQueryableAsync()).Include(i => i.DataList);
        return query.Paginate(page, pageSize, route, configuration, queryParams);
    }

    public async Task<Instance> GetActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var instance = await GetAsync(id, true, cancellationToken);
        if (instance.Status.Equals(InstanceStatus.Completed))
        {
            throw new InstanceCompletedException(instance.Id);
        }

        return instance;
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
}