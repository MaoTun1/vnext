using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Aether.Domain.Services;
using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore;

namespace BBT.Workflow.Instances;

public sealed class EfCoreInstanceJobRepository(
    WorkflowDbContext dbContext,
    IServiceProvider serviceProvider,
    ITransactionService transactionService)
    : EfCoreRepository<WorkflowDbContext, InstanceJob, Guid>(dbContext, serviceProvider, transactionService),
        IInstanceJobRepository
{
    public async Task<bool> ExistsAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return await
            (await GetQueryableAsync())
            .AnyAsync(a =>
                    a.JobId == jobId,
                cancellationToken: cancellationToken);
    }

    public async Task<InstanceJob?> FindByNameAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return await
            (await GetQueryableAsync())
            .FirstOrDefaultAsync(a =>
                    a.JobId == jobId,
                cancellationToken: cancellationToken);
    }

    public async Task<List<InstanceJob>> GetListUntriggeredAsync(CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Where(p => p.IsTriggered == false)
            .ToListAsync(cancellationToken);
    }
}