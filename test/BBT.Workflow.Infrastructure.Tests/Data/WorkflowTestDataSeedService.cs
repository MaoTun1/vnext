using System;
using System.Threading.Tasks;
using BBT.Aether.Domain.Services;

namespace BBT.Workflow.Data;

/// <summary>
/// This service is responsible for seeding test data into the repositories.
/// It implements the <see cref="IDataSeedService"/> interface.
/// </summary>
public class WorkflowTestDataSeedService() : IDataSeedService
{
    /// <summary>
    /// Seeds the data asynchronously.
    /// </summary>
    /// <param name="context">The seed context.</param>
    public Task SeedAsync(SeedContext context)
    {
        return Task.CompletedTask;
    }

   
}