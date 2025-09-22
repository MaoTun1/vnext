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
    /// <inheritdoc />
    public async Task<List<InstanceCorrelation>> GetActiveByParentAsync(
        Guid parentInstanceId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AsNoTracking()
            .Where(c => c.ParentInstanceId == parentInstanceId && !c.IsCompleted)
            .ToListAsync(cancellationToken);
    }

     /// <inheritdoc />
    public async Task<bool> AnyActiveSubFlowByParentAsync(Guid parentInstanceId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(c =>
                c.ParentInstanceId == parentInstanceId
                && !c.IsCompleted
                && c.SubFlowType == SubFlowType.SubFlow)
            .AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InstanceCorrelation?> FindActiveSubFlowByParentAsync(Guid parentInstanceId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(c =>
                    c.ParentInstanceId == parentInstanceId
                    && !c.IsCompleted
                    && c.SubFlowType == SubFlowType.SubFlow,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InstanceCorrelation?> FindBySubInstanceIdAsync(
        Guid subInstanceId, 
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(c => c.SubFlowInstanceId == subInstanceId, cancellationToken);
    }
}