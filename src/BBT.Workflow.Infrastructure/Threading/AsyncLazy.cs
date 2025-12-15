namespace BBT.Workflow.Infrastructure.Threading;

/// <summary>
/// Provides thread-safe async lazy initialization.
/// Ensures the factory function is called only once, even in concurrent scenarios.
/// </summary>
/// <typeparam name="T">The type of the lazily initialized value.</typeparam>
public sealed class AsyncLazy<T>
{
    private readonly object _sync = new();
    private Func<CancellationToken, Task<T>>? _factory;
    private Task<T>? _valueTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
    /// </summary>
    /// <param name="factory">The async factory function that produces the value.</param>
    /// <exception cref="ArgumentNullException">Thrown when factory is null.</exception>
    public AsyncLazy(Func<CancellationToken, Task<T>> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Gets the lazily initialized value, or initializes it if not yet done.
    /// Thread-safe: multiple concurrent calls will return the same task.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the initialization operation.</param>
    /// <returns>A task representing the lazily initialized value.</returns>
    public Task<T> GetValueAsync(CancellationToken cancellationToken = default)
    {
        // Fast path - value already initialized
        var task = _valueTask;
        if (task is not null)
            return task;

        lock (_sync)
        {
            task = _valueTask;
            if (task is not null)
                return task;

            var factory = _factory ?? throw new InvalidOperationException("Factory already consumed.");
            _factory = null;

            task = factory(cancellationToken);
            _valueTask = task;

            return task;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the value has been created.
    /// </summary>
    public bool IsValueCreated => _valueTask?.IsCompletedSuccessfully == true;
}

