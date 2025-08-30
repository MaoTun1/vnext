using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Aether.Domain.Services;
using BBT.Workflow.Data;
using BBT.Workflow.Definitions;
using Microsoft.EntityFrameworkCore;

namespace BBT.Workflow.Instances;

/// <summary>
/// Entity Framework Core implementation of the IInstanceCorrelationRepository interface.
/// This repository provides data access operations for managing workflow instance correlations
/// using Entity Framework Core and PostgreSQL.
/// </summary>
/// <param name="dbContext">The workflow database context for data operations.</param>
/// <param name="serviceProvider">Service provider for dependency injection.</param>
/// <param name="transactionService">Service for managing database transactions.</param>
public sealed class EfCoreInstanceCorrelationRepository(
    WorkflowDbContext dbContext,
    IServiceProvider serviceProvider,
    ITransactionService transactionService)
    : EfCoreRepository<WorkflowDbContext, InstanceCorrelation, Guid>(dbContext, serviceProvider, transactionService),
        IInstanceCorrelationRepository
{
    /// <summary>
    /// Finds active correlations where the specified instance ID is the parent instance.
    /// This method returns correlations that are not yet completed.
    /// </summary>
    /// <param name="parentInstanceId">The unique identifier of the parent workflow instance.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The result contains a collection of active correlations where the instance is a parent.
    /// </returns>
    public async Task<List<InstanceCorrelation>> GetActiveByParentAsync(
        Guid parentInstanceId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AsNoTracking()
            .Where(c => c.ParentInstanceId == parentInstanceId && !c.IsCompleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AnyActiveByParentAsync(Guid parentInstanceId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(c =>
                c.ParentInstanceId == parentInstanceId
                && !c.IsCompleted
                && c.SubFlowType == SubFlowType.SubFlow)
            .AnyAsync(cancellationToken);
    }

    public async Task<InstanceCorrelation?> FindActiveByParentAsync(Guid parentInstanceId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(c =>
                    c.ParentInstanceId == parentInstanceId
                    && !c.IsCompleted
                    && c.SubFlowType == SubFlowType.SubFlow,
                cancellationToken);
    }
}