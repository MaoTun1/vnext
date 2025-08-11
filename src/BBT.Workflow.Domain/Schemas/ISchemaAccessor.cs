namespace BBT.Workflow.Schemas;

/// <summary>
/// Provides access to the current database schema context.
/// This interface abstracts the mechanism for getting and setting the current schema name.
/// </summary>
public interface ISchemaAccessor
{
    /// <summary>
    /// Gets or sets the current schema name for the current execution context.
    /// </summary>
    /// <value>
    /// The name of the current database schema, or <c>null</c> if no schema is set.
    /// When set to <c>null</c>, the default schema will be used.
    /// </value>
    string? Current { get; set; }
}