namespace BBT.Workflow.Schemas;

/// <summary>
/// Provides functionality for managing the current database schema context.
/// This interface allows retrieving the current schema name and temporarily changing it within a scope.
/// </summary>
public interface ICurrentSchema
{
    /// <summary>
    /// Gets the name of the current database schema.
    /// </summary>
    /// <value>
    /// The name of the current schema. Returns "public" as the default schema name 
    /// if no schema is currently set in the context.
    /// </value>
    string Name { get; }

    /// <summary>
    /// Temporarily changes the current schema to the specified name within a disposable scope.
    /// When the returned <see cref="IDisposable"/> is disposed, the schema context is restored to its previous state.
    /// </summary>
    /// <param name="name">The name of the schema to temporarily set as current. The name will be sanitized to contain only letters, digits, and underscores.</param>
    /// <returns>
    /// An <see cref="IDisposable"/> object that, when disposed, restores the previous schema context.
    /// This enables using the method with the 'using' statement for automatic cleanup.
    /// </returns>
    /// <example>
    /// <code>
    /// using (currentSchema.Change("tenant_schema"))
    /// {
    ///     // Operations here will use "tenant_schema"
    /// }
    /// // Schema is automatically restored to previous value
    /// </code>
    /// </example>
    IDisposable Change(
        string name);
}