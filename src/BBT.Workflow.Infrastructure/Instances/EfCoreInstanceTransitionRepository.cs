using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Aether.Domain.Services;
using BBT.Workflow.Data;
using BBT.Workflow.DataSink;

namespace BBT.Workflow.Instances;

public class EfCoreInstanceTransitionRepository(
    WorkflowDbContext dbContext,
    IServiceProvider serviceProvider,
    ITransactionService transactionService,
    IDataSinkManager dataSinkManager)
    : EfCoreRepository<WorkflowDbContext, InstanceTransition, Guid>(dbContext, serviceProvider, transactionService),
        IInstanceTransitionRepository
{
    /// <summary>
    /// Inserts a new instance transition and transfers to data sinks
    /// </summary>
    public override async Task<InstanceTransition> InsertAsync(InstanceTransition entity, bool autoSave = false, CancellationToken cancellationToken = default)
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
            Console.WriteLine($"Failed to transfer instance transition to data sinks: {ex.Message}");
        }
        
        return result;
    }

    /// <summary>
    /// Updates an instance transition and transfers to data sinks
    /// </summary>
    public override async Task<InstanceTransition> UpdateAsync(InstanceTransition entity, bool autoSave = false, CancellationToken cancellationToken = default)
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
            Console.WriteLine($"Failed to transfer instance transition to data sinks: {ex.Message}");
        }
        
        return result;
    }
}