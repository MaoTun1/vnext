using BBT.Aether.Domain.EntityFrameworkCore;
using BBT.Aether.Domain.Services;
using BBT.Workflow.Data;

namespace BBT.Workflow.Instances;

public class EfCoreInstanceTransitionRepository(
    WorkflowDbContext dbContext,
    IServiceProvider serviceProvider,
    ITransactionService transactionService)
    : EfCoreRepository<WorkflowDbContext, InstanceTransition, Guid>(dbContext, serviceProvider, transactionService),
        IInstanceTransitionRepository;