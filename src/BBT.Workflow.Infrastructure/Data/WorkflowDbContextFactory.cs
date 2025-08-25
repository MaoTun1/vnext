using BBT.Workflow.Schemas;
using BBT.Workflow.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BBT.Workflow.Data;

public class WorkflowDbContextFactory(
    ICurrentSchema currentSchema,
    DbContextOptions<WorkflowDbContext> options,
    WorkflowDatabaseInterceptor databaseInterceptor,
    WorkflowTransactionInterceptor transactionInterceptor)
    : IDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var builder = new DbContextOptionsBuilder<WorkflowDbContext>(options);

        builder.UseNpgsql(
            options.Extensions.OfType<RelationalOptionsExtension>().First().ConnectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__Workflow_Migrations", currentSchema.Name);
                // Enable retrying failed database operations
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            });

        builder.ReplaceService<IModelCacheKeyFactory, DynamicSchemaModelCacheKeyFactory>();
        builder.ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>();
        
        // Add database and transaction metrics interceptors
        builder.AddInterceptors(databaseInterceptor, transactionInterceptor);
        
        return new WorkflowDbContext(builder.Options, currentSchema);
    }
}