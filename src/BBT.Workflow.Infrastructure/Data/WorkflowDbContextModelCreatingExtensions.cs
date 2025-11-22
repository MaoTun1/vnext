using Microsoft.EntityFrameworkCore;

namespace BBT.Workflow.Data;

public static class WorkflowDbContextModelCreatingExtensions
{
    public static void ConfigureWorkflow(
        this ModelBuilder builder, string? schema = null)
    {
        /* Configure all entities here. */

        builder.ConfigureInstances(schema);
    }
}