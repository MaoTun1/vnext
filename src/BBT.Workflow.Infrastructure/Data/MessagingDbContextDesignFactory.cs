using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BBT.Workflow.Data;

public sealed class MessagingDbContextDesignFactory: IDesignTimeDbContextFactory<MessagingDbContext>
{
    public MessagingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MessagingDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=Aether_WorkflowDb;Username=postgres;Password=postgres;",
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__Workflow_Migrations", "sys_queues");
            });

        return new MessagingDbContext(
            optionsBuilder.Options
        );
    }
}