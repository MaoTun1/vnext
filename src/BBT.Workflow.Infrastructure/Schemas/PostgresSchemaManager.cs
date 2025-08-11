using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore;

namespace BBT.Workflow.Schemas;

/// <summary>
/// Provides PostgreSQL-specific implementation for managing database schemas and ensuring database structure consistency.
/// This class implements the <see cref="ISchemaManager"/> interface to handle schema creation, table initialization,
/// and database migration operations specifically for PostgreSQL database systems.
/// </summary>
/// <remarks>
/// The PostgresSchemaManager is designed to work with the workflow system's multi-schema architecture,
/// ensuring that database schemas are properly created and migrated before workflow operations begin.
/// It integrates with Entity Framework Core's migration system to apply pending database changes automatically.
/// 
/// This implementation is particularly useful in multi-tenant scenarios where each tenant or workflow
/// may operate within its own database schema, requiring dynamic schema management capabilities.
/// </remarks>
/// <param name="dbContextFactory">
/// The factory service for creating <see cref="WorkflowDbContext"/> instances. This factory ensures that
/// database contexts are created with the appropriate configuration and connection settings for the current operation.
/// </param>
public class PostgresSchemaManager(
    IDbContextFactory<WorkflowDbContext> dbContextFactory)
    : ISchemaManager
{
    /// <summary>
    /// Ensures that the specified database schema exists and applies any pending database migrations.
    /// This method creates the necessary database structure and tables for the workflow system to operate correctly.
    /// </summary>
    /// <param name="schemaName">
    /// The name of the database schema to ensure exists. While the parameter is provided for interface compliance,
    /// the actual schema name is typically determined by the current schema context within the <see cref="WorkflowDbContext"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation of ensuring schema and table existence.
    /// The task completes when all pending migrations have been applied and the database structure is ready for use.
    /// </returns>
    /// <remarks>
    /// The method performs the following operations:
    /// 1. Creates a new <see cref="WorkflowDbContext"/> instance using the provided factory
    /// 2. Checks for any pending database migrations using Entity Framework Core's migration system
    /// 3. If pending migrations are found, applies them to bring the database schema up to date
    /// 4. Ensures that all necessary tables and database objects are created within the target schema
    /// 
    /// This approach provides automatic database schema evolution and ensures that the workflow system
    /// can operate with the latest database structure without manual intervention.
    /// 
    /// Note: The actual schema name used for operations is determined by the <see cref="WorkflowDbContext"/>
    /// configuration and the current schema context, rather than the schemaName parameter directly.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// May be thrown if the database migration process encounters errors or if the database connection cannot be established.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the <paramref name="cancellationToken"/>.
    /// </exception>
    public async Task EnsureSchemaAndTablesAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Any())
        {
            await context.Database.MigrateAsync(cancellationToken);     
        }
    }

    /// <summary>
    /// Checks whether the specified database schema exists in the PostgreSQL database.
    /// This method performs a read-only operation to verify schema availability without attempting to create it.
    /// </summary>
    /// <param name="schemaName">
    /// The name of the database schema to check for existence. This parameter is used to identify
    /// the specific schema within the PostgreSQL database.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="Task{Boolean}"/> that represents the asynchronous operation, containing true if the schema exists; otherwise, false.
    /// </returns>
    /// <remarks>
    /// The method performs the following operations:
    /// 1. Creates a new <see cref="WorkflowDbContext"/> instance using the provided factory
    /// 2. Executes a direct SQL query against the PostgreSQL information_schema.schemata table
    /// 3. Returns true if the schema is found, false otherwise
    /// 
    /// This method is particularly useful for execution applications that need to verify schema existence
    /// without triggering migration operations, preventing race conditions in multi-application scenarios.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="schemaName"/> is null or empty.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the <paramref name="cancellationToken"/>.
    /// </exception>
    public async Task<bool> SchemaExistsAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
            throw new ArgumentNullException(nameof(schemaName));

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        // Query PostgreSQL information_schema to check schema existence
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = @schemaName";
        
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@schemaName";
        parameter.Value = schemaName;
        command.Parameters.Add(parameter);
        
        var result = await command.ExecuteScalarAsync(cancellationToken);
        
        return Convert.ToInt64(result ?? 0) > 0;
    }
}