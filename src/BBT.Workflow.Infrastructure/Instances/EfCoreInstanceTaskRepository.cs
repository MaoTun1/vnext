using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Aether.Domain.Services;
using BBT.Workflow.ClickHouse;
using BBT.Workflow.Data;

namespace BBT.Workflow.Instances;

public class EfCoreInstanceTaskRepository(
    WorkflowDbContext dbContext,
    IServiceProvider serviceProvider,
    ITransactionService transactionService,
    IClickHouseDataTransfer? clickHouseDataTransfer = null)
    : EfCoreRepository<WorkflowDbContext, InstanceTask, Guid>(dbContext, serviceProvider, transactionService),
        IInstanceTaskRepository
{
    /// <summary>
    /// Inserts a new instance task and transfers to ClickHouse
    /// </summary>
    public override async Task<InstanceTask> InsertAsync(InstanceTask entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var result = await base.InsertAsync(entity, autoSave, cancellationToken);
        
        // Transfer to ClickHouse if enabled
        if (clickHouseDataTransfer != null)
        {
            try
            {
                await clickHouseDataTransfer.TransferInstanceTaskAsync(result, DataTransferOperation.Insert, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the main operation
                Console.WriteLine($"Failed to transfer instance task to ClickHouse: {ex.Message}");
            }
        }
        
        return result;
    }

    /// <summary>
    /// Updates an instance task and transfers to ClickHouse
    /// </summary>
    public override async Task<InstanceTask> UpdateAsync(InstanceTask entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var result = await base.UpdateAsync(entity, autoSave, cancellationToken);
        
        // Transfer to ClickHouse if enabled
        if (clickHouseDataTransfer != null)
        {
            try
            {
                await clickHouseDataTransfer.TransferInstanceTaskAsync(result, DataTransferOperation.Update, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the main operation
                Console.WriteLine($"Failed to transfer instance task to ClickHouse: {ex.Message}");
            }
        }
        
        return result;
    }
}