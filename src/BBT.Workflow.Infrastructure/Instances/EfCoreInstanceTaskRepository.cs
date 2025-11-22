using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Aether.Domain.Services;
using BBT.Aether.Uow;
using BBT.Workflow.Data;
using BBT.Workflow.DataSink;

namespace BBT.Workflow.Instances;

public class EfCoreInstanceTaskRepository(
    WorkflowDbContext dbContext,
    IServiceProvider serviceProvider,
    IDataSinkManager dataSinkManager)
    : EfCoreRepository<WorkflowDbContext, InstanceTask, Guid>(dbContext, serviceProvider),
        IInstanceTaskRepository
{
    /// <summary>
    /// Inserts a new instance task and transfers to data sinks
    /// </summary>
    public override async Task<InstanceTask> InsertAsync(InstanceTask entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var result = await base.InsertAsync(entity, autoSave, cancellationToken);
        
        // Transfer to data sinks (e.g., ClickHouse) if enabled
        try
        {
            await dataSinkManager.HandleInsertAsync(result, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the main operation
            Console.WriteLine($"Failed to transfer instance task to data sinks: {ex.Message}");
        }
        
        return result;
    }

    /// <summary>
    /// Updates an instance task and transfers to data sinks
    /// </summary>
    public override async Task<InstanceTask> UpdateAsync(InstanceTask entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var result = await base.UpdateAsync(entity, autoSave, cancellationToken);
        
        // Transfer to data sinks (e.g., ClickHouse) if enabled
        try
        {
            await dataSinkManager.HandleUpdateAsync(result, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the main operation
            Console.WriteLine($"Failed to transfer instance task to data sinks: {ex.Message}");
        }
        
        return result;
    }
}