using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BBT.Workflow.Data;

public sealed class WorkflowDbContextDesignFactory : IDesignTimeDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WorkflowDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=Aether_WorkflowDb;Username=postgres;Password=postgres;",
            npgsqlOptions => { npgsqlOptions.MigrationsHistoryTable("__Workflow_Migrations"); });

        return new WorkflowDbContext(
            optionsBuilder.Options
        );
    }
}