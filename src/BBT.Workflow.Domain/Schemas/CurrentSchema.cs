using BBT.Aether;

namespace BBT.Workflow.Schemas;

/// <summary>
/// Provides implementation for managing the current database schema context.
/// This class uses an <see cref="ISchemaAccessor"/> to maintain schema state and allows temporary schema changes.
/// </summary>
/// <param name="schemaAccessor">The schema accessor used to get and set the current schema context.</param>
public class CurrentSchema(ISchemaAccessor schemaAccessor) : ICurrentSchema
{
    /// <summary>
    /// Gets the name of the current database schema.
    /// </summary>
    /// <value>
    /// Returns the current schema name from the accessor, or "public" if no schema is set.
    /// </value>
    public string Name => schemaAccessor.Current ?? "public";

    /// <summary>
    /// Temporarily changes the current schema to the specified name within a disposable scope.
    /// The schema name is sanitized to ensure it contains only valid characters (letters, digits, underscores).
    /// </summary>
    /// <param name="name">The name of the schema to temporarily set as current.</param>
    /// <returns>
    /// An <see cref="IDisposable"/> that restores the previous schema context when disposed.
    /// </returns>
    public IDisposable Change(string name)
    {
        return SetCurrent(name);
    }

    /// <summary>
    /// Sets the current schema and returns a disposable object that will restore the previous schema when disposed.
    /// This method sanitizes the schema name by replacing invalid characters with underscores.
    /// </summary>
    /// <param name="name">The schema name to set. Can be null, in which case the schema will be cleared.</param>
    /// <returns>
    /// An <see cref="IDisposable"/> that will restore the previous schema context when disposed.
    /// </returns>
    private IDisposable SetCurrent(
        string name
    )
    {
        var parentScope = schemaAccessor.Current;
        schemaAccessor.Current = new string(name?.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return new DisposeAction(() => { schemaAccessor.Current = parentScope; });
    }
}