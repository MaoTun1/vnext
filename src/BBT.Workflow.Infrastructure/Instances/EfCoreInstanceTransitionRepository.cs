using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Aether.Domain.Services;
using BBT.Workflow.ClickHouse;
using BBT.Workflow.Data;

namespace BBT.Workflow.Instances;

public class EfCoreInstanceTransitionRepository(
    WorkflowDbContext dbContext,
    IServiceProvider serviceProvider,
    ITransactionService transactionService,
    IClickHouseDataTransfer? clickHouseDataTransfer = null)
    : EfCoreRepository<WorkflowDbContext, InstanceTransition, Guid>(dbContext, serviceProvider, transactionService),
        IInstanceTransitionRepository
{
    /// <summary>
    /// Inserts a new instance transition and transfers to ClickHouse
    /// </summary>
    public override async Task<InstanceTransition> InsertAsync(InstanceTransition entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var result = await base.InsertAsync(entity, autoSave, cancellationToken);
        
        // Transfer to ClickHouse if enabled
        if (clickHouseDataTransfer != null)
        {
            try
            {
                await clickHouseDataTransfer.TransferInstanceTransitionAsync(result, DataTransferOperation.Insert, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the main operation
                Console.WriteLine($"Failed to transfer instance transition to ClickHouse: {ex.Message}");
            }
        }
        
        return result;
    }

    /// <summary>
    /// Updates an instance transition and transfers to ClickHouse
    /// </summary>
    public override async Task<InstanceTransition> UpdateAsync(InstanceTransition entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        var result = await base.UpdateAsync(entity, autoSave, cancellationToken);
        
        // Transfer to ClickHouse if enabled
        if (clickHouseDataTransfer != null)
        {
            try
            {
                await clickHouseDataTransfer.TransferInstanceTransitionAsync(result, DataTransferOperation.Update, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the main operation
                Console.WriteLine($"Failed to transfer instance transition to ClickHouse: {ex.Message}");
            }
        }
        
        return result;
    }
}