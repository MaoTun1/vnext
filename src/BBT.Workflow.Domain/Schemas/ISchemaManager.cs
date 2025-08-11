namespace BBT.Workflow.Schemas;

/// <summary>
/// Defines operations for managing database schemas in the workflow system.
/// Provides functionality to ensure schema existence and table creation.
/// </summary>
public interface ISchemaManager
{
    /// <summary>
    /// Ensures that the specified database schema exists and creates necessary tables if they don't exist.
    /// This method is typically used for database initialization and migration purposes.
    /// </summary>
    /// <param name="schemaName">The name of the database schema to ensure exists. Must be a valid schema name.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation. Default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation of ensuring schema and tables existence.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="schemaName"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the schema cannot be created or accessed.</exception>
    Task EnsureSchemaAndTablesAsync(string schemaName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the specified database schema exists without attempting to create it.
    /// This method is used for read-only operations to verify schema availability.
    /// </summary>
    /// <param name="schemaName">The name of the database schema to check for existence.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation. Default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation, containing true if the schema exists; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="schemaName"/> is null or empty.</exception>
    Task<bool> SchemaExistsAsync(string schemaName, CancellationToken cancellationToken = default);
}