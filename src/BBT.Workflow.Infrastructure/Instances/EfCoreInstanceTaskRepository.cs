using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Aether.Domain.Services;
using BBT.Workflow.Data;

namespace BBT.Workflow.Instances;

public class EfCoreInstanceTaskRepository(
    WorkflowDbContext dbContext,
    IServiceProvider serviceProvider,
    ITransactionService transactionService)
    : EfCoreRepository<WorkflowDbContext, InstanceTask, Guid>(dbContext, serviceProvider, transactionService),
        IInstanceTaskRepository;