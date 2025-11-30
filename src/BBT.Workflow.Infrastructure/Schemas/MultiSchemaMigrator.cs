using BBT.Aether.MultiSchema;
using Microsoft.EntityFrameworkCore;

namespace BBT.Workflow.Schemas;

public sealed class MultiSchemaMigrator<TContext>(
    TContext dbContext,
    ISchemaNameFormatter schemaNameFormatter
) : IMultiSchemaMigrator<TContext>
    where TContext : DbContext
{
    public async Task MigrateSchemaAsync(string schema, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schema))
            throw new ArgumentException("Schema name cannot be null or empty.", nameof(schema));

        var schemaName = schemaNameFormatter.Format(schema);
        var createSchemaSql = $@"CREATE SCHEMA IF NOT EXISTS ""{schemaName}"";";
        await dbContext.Database.ExecuteSqlRawAsync(createSchemaSql, cancellationToken);
        
        var hasPendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (hasPendingMigrations.Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }
}