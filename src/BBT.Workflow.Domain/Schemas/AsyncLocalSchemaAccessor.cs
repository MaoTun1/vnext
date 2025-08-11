namespace BBT.Workflow.Schemas;

/// <summary>
/// Provides thread-safe access to the current database schema using AsyncLocal storage.
/// This implementation ensures that schema context is maintained across async operations within the same logical thread.
/// Uses the singleton pattern to provide a global instance for schema context management.
/// </summary>
public class AsyncLocalSchemaAccessor : ISchemaAccessor
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="AsyncLocalSchemaAccessor"/>.
    /// This instance provides global access to the schema context within the application.
    /// </summary>
    /// <value>
    /// The singleton instance that can be used throughout the application to access and modify schema context.
    /// </value>
    public static AsyncLocalSchemaAccessor Instance { get; } = new();

    /// <summary>
    /// Gets or sets the current schema name for the current async execution context.
    /// This property uses <see cref="AsyncLocal{T}"/> to maintain schema context across async operations.
    /// </summary>
    /// <value>
    /// The name of the current database schema for this execution context, or <c>null</c> if no schema is set.
    /// Setting this value will only affect the current logical thread and its child async operations.
    /// </value>
    public string? Current
    {
        get => _currentScope.Value;
        set => _currentScope.Value = value;
    }

    /// <summary>
    /// The AsyncLocal storage that maintains the schema context across async operations.
    /// </summary>
    private readonly AsyncLocal<string?> _currentScope;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLocalSchemaAccessor"/> class.
    /// This constructor is private to enforce the singleton pattern.
    /// </summary>
    private AsyncLocalSchemaAccessor()
    {
        _currentScope = new AsyncLocal<string?>();
    }
}