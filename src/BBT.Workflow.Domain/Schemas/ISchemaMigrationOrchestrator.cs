namespace BBT.Workflow.Schemas;

/// <summary>
/// Orchestrates schema migrations with distributed locking to ensure safe concurrent execution.
/// This service wraps the migration process with distributed locking mechanism to prevent
/// multiple instances from migrating the same schema simultaneously.
/// </summary>
public interface ISchemaMigrationOrchestrator
{
    /// <summary>
    /// Migrates a database schema with distributed lock protection.
    /// Only one instance across the distributed system can migrate a given schema at a time.
    /// </summary>
    /// <param name="schemaName">The name of the schema to migrate.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation, containing a boolean value:
    /// - true if the migration was executed successfully
    /// - false if the lock could not be acquired (another instance is migrating this schema)
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when schemaName is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when migration fails.</exception>
    Task<bool> MigrateSchemaWithLockAsync(string schemaName, CancellationToken cancellationToken = default);
}

