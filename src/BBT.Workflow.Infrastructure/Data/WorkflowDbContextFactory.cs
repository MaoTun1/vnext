using Microsoft.EntityFrameworkCore;

namespace BBT.Workflow.Data;

public class WorkflowDbContextFactory(
    DbContextOptions<WorkflowDbContext> options)
    : IDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext()
    {
        return new WorkflowDbContext(options);
    }
}