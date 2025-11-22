using BBT.Aether.BackgroundJob;
using BBT.Aether.Clock;
using BBT.Aether.Domain.Repositories;
using BBT.Aether.Uow;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.BackgroundJob;

public class AppJobDispatcher(
    IServiceProvider serviceProvider,
    IJobStore jobStore,
    IUnitOfWorkManager uowManager,
    BackgroundJobOptions options,
    IClock clock,
    ILogger<JobDispatcher> logger,
    IInstanceJobRepository jobRepository,
    ICurrentSchema currentSchema) : JobDispatcher(serviceProvider, jobStore, uowManager, options, clock, logger)
{
    public override async Task DispatchAsync(Guid jobId, string handlerName, ReadOnlyMemory<byte> jobPayload,
        CancellationToken cancellationToken = default)
    {
        var job = await jobRepository.FindByJobIdAsReadOnlyAsync(jobId, cancellationToken);
        if (job == null)
        {
            await base.DispatchAsync(jobId, handlerName, jobPayload, cancellationToken);
        }
        else
        {
            using (currentSchema.Change(job.FlowName))
            {
                await base.DispatchAsync(jobId, handlerName, jobPayload, cancellationToken);
            }
        }
    }
}