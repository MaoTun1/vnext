namespace BBT.Workflow.Domain;

/// <summary>
/// Provides monadic extension methods for Result types enabling fluent, LINQ-style composition.
/// These extensions support functional programming patterns like Map, Bind, and Tap.
/// </summary>
public static class ResultExtensions
{
    #region Synchronous Operations

    /// <summary>
    /// Maps a successful result to a new result by applying a transformation function.
    /// If the result is failed, the error is propagated.
    /// </summary>
    /// <typeparam name="T">Source value type</typeparam>
    /// <typeparam name="TU">Target value type</typeparam>
    /// <param name="result">The source result</param>
    /// <param name="mapper">Function to transform the value</param>
    /// <returns>A new result with the transformed value or the original error</returns>
    public static Result<TU> Map<T, TU>(this Result<T> result, Func<T, TU> mapper)
        => result.IsSuccess ? Result<TU>.Ok(mapper(result.Value!)) : Result<TU>.Fail(result.Error);

    /// <summary>
    /// Binds a successful result to a new result-returning function (flatMap).
    /// If the result is failed, the error is propagated without executing the binder.
    /// </summary>
    /// <typeparam name="T">Source value type</typeparam>
    /// <typeparam name="TU">Target value type</typeparam>
    /// <param name="result">The source result</param>
    /// <param name="binder">Function that returns a new Result</param>
    /// <returns>The result from the binder function or the original error</returns>
    public static Result<TU> Bind<T, TU>(this Result<T> result, Func<T, Result<TU>> binder)
        => result.IsSuccess ? binder(result.Value!) : Result<TU>.Fail(result.Error);

    /// <summary>
    /// Executes a side effect if the result is successful, then returns the result unchanged.
    /// Useful for logging, notifications, or other actions that don't transform the result.
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="result">The source result</param>
    /// <param name="sideEffect">Action to perform on success</param>
    /// <returns>The original result</returns>
    public static Result<T> Tap<T>(this Result<T> result, Action<T> sideEffect)
    {
        if (result.IsSuccess) sideEffect(result.Value!);
        return result;
    }

    /// <summary>
    /// Ensures a predicate is satisfied, otherwise returns an error.
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="result">The source result</param>
    /// <param name="predicate">Condition that must be true</param>
    /// <param name="error">Error to return if predicate fails</param>
    /// <returns>The original result if predicate passes, otherwise a failure</returns>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
        => result.IsSuccess && !predicate(result.Value!) ? Result<T>.Fail(error) : result;

    /// <summary>
    /// Matches the result to execute different actions based on success or failure.
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <typeparam name="TU">Return type</typeparam>
    /// <param name="result">The source result</param>
    /// <param name="onSuccess">Function to execute on success</param>
    /// <param name="onFailure">Function to execute on failure</param>
    /// <returns>The result of either function</returns>
    public static TU Match<T, TU>(this Result<T> result, Func<T, TU> onSuccess, Func<Error, TU> onFailure)
        => result.IsSuccess ? onSuccess(result.Value!) : onFailure(result.Error);

    #endregion

    #region Async Operations (Task-based)

    /// <summary>
    /// Async version of Map for Task-wrapped results.
    /// </summary>
    public static async Task<Result<TU>> MapAsync<T, TU>(this Task<Result<T>> task, Func<T, TU> mapper)
        => (await task).Map(mapper);

    /// <summary>
    /// Async version of Map where the mapper itself is async.
    /// </summary>
    public static async Task<Result<TU>> MapAsync<T, TU>(this Task<Result<T>> task, Func<T, Task<TU>> mapper)
    {
        var result = await task;
        if (!result.IsSuccess) return Result<TU>.Fail(result.Error);
        var value = await mapper(result.Value!);
        return Result<TU>.Ok(value);
    }

    /// <summary>
    /// Async version of Bind for Task-wrapped results.
    /// </summary>
    public static async Task<Result<TU>> BindAsync<T, TU>(this Task<Result<T>> task, Func<T, Task<Result<TU>>> binder)
    {
        var result = await task;
        return result.IsSuccess ? await binder(result.Value!) : Result<TU>.Fail(result.Error);
    }

    /// <summary>
    /// Async version of Tap for Task-wrapped results.
    /// </summary>
    public static async Task<Result<T>> TapAsync<T>(this Task<Result<T>> task, Func<T, Task> sideEffect)
    {
        var result = await task;
        if (result.IsSuccess) await sideEffect(result.Value!);
        return result;
    }

    /// <summary>
    /// Async version of Ensure for Task-wrapped results.
    /// </summary>
    public static async Task<Result<T>> EnsureAsync<T>(this Task<Result<T>> task, Func<T, bool> predicate, Error error)
    {
        var result = await task;
        return result.Ensure(predicate, error);
    }

    /// <summary>
    /// Async version of Match for Task-wrapped results.
    /// </summary>
    public static async Task<TU> MatchAsync<T, TU>(this Task<Result<T>> task, Func<T, TU> onSuccess, Func<Error, TU> onFailure)
    {
        var result = await task;
        return result.Match(onSuccess, onFailure);
    }

    #endregion

    #region Exception Handling

    /// <summary>
    /// Wraps a potentially throwing operation in a Result.
    /// Converts exceptions to appropriate Error types.
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="action">The operation to execute</param>
    /// <param name="errorMapper">Optional custom exception to error mapper</param>
    /// <returns>A Result containing either the value or an error</returns>
    public static Result<T> Try<T>(Func<T> action, Func<Exception, Error>? errorMapper = null)
    {
        try 
        { 
            return Result<T>.Ok(action()); 
        }
        catch (OperationCanceledException ex) 
        { 
            return Result<T>.Fail(Error.Transient("canceled", ex.Message)); 
        }
        catch (Exception ex) 
        { 
            return Result<T>.Fail(errorMapper?.Invoke(ex) ?? Error.Dependency("unexpected", ex.Message)); 
        }
    }

    /// <summary>
    /// Wraps a non-generic potentially throwing operation in a Result.
    /// </summary>
    public static Result Try(Action action, Func<Exception, Error>? errorMapper = null)
    {
        try 
        { 
            action(); 
            return Result.Ok(); 
        }
        catch (OperationCanceledException ex) 
        { 
            return Result.Fail(Error.Transient("canceled", ex.Message)); 
        }
        catch (Exception ex) 
        { 
            return Result.Fail(errorMapper?.Invoke(ex) ?? Error.Dependency("unexpected", ex.Message)); 
        }
    }

    /// <summary>
    /// Async version of Try for async operations.
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(
        Func<CancellationToken, Task<T>> action, 
        CancellationToken cancellationToken,
        Func<Exception, Error>? errorMapper = null)
    {
        try 
        { 
            return Result<T>.Ok(await action(cancellationToken)); 
        }
        catch (OperationCanceledException ex) 
        { 
            return Result<T>.Fail(Error.Transient("canceled", ex.Message)); 
        }
        catch (Exception ex) 
        { 
            return Result<T>.Fail(errorMapper?.Invoke(ex) ?? Error.Dependency("unexpected", ex.Message)); 
        }
    }

    /// <summary>
    /// Async version of Try for non-generic async operations.
    /// </summary>
    public static async Task<Result> TryAsync(
        Func<CancellationToken, Task> action, 
        CancellationToken cancellationToken,
        Func<Exception, Error>? errorMapper = null)
    {
        try 
        { 
            await action(cancellationToken);
            return Result.Ok(); 
        }
        catch (OperationCanceledException ex) 
        { 
            return Result.Fail(Error.Transient("canceled", ex.Message)); 
        }
        catch (Exception ex) 
        { 
            return Result.Fail(errorMapper?.Invoke(ex) ?? Error.Dependency("unexpected", ex.Message)); 
        }
    }

    #endregion

    #region Combining Results

    /// <summary>
    /// Combines multiple results into a single result.
    /// Returns the first error encountered, or Ok if all succeed.
    /// </summary>
    public static Result Combine(params Result[] results)
    {
        foreach (var result in results)
            if (!result.IsSuccess) return result;
        return Result.Ok();
    }

    /// <summary>
    /// Combines multiple Result&lt;T&gt; into Result&lt;T[]&gt;.
    /// Returns the first error encountered, or an array of all values if all succeed.
    /// </summary>
    public static Result<T[]> WhenAll<T>(IEnumerable<Result<T>> results)
    {
        var list = new List<T>();
        foreach (var result in results)
        {
            if (!result.IsSuccess) 
                return Result<T[]>.Fail(result.Error);
            list.Add(result.Value!);
        }
        return Result<T[]>.Ok(list.ToArray());
    }

    /// <summary>
    /// Combines multiple Task&lt;Result&lt;T&gt;&gt; into Task&lt;Result&lt;T[]&gt;&gt;.
    /// </summary>
    public static async Task<Result<T[]>> WhenAllAsync<T>(IEnumerable<Task<Result<T>>> tasks)
    {
        var results = await Task.WhenAll(tasks);
        return WhenAll(results);
    }

    #endregion

    #region Railway Oriented Programming

    /// <summary>
    /// Railway operator: Chains async operations, propagating errors along the way.
    /// </summary>
    /// <typeparam name="TIn">Input value type</typeparam>
    /// <typeparam name="TOut">Output value type</typeparam>
    /// <param name="resultTask">The source result task</param>
    /// <param name="next">The next operation to execute on success</param>
    /// <returns>The result from the next operation or the original error</returns>
    /// <example>
    /// await GetWorkflowAsync()
    ///     .ThenAsync(workflow => CreateInstanceAsync(workflow))
    ///     .ThenAsync(instance => ExecuteTransitionAsync(instance))
    ///     .ThenAsync(output => BuildResponseAsync(output));
    /// </example>
    public static async Task<Result<TOut>> ThenAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<Result<TOut>>> next)
    {
        var result = await resultTask;
        return result.IsSuccess 
            ? await next(result.Value!) 
            : Result<TOut>.Fail(result.Error);
    }

    /// <summary>
    /// Railway operator: Chains async operations with synchronous next step.
    /// </summary>
    public static async Task<Result<TOut>> ThenAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Result<TOut>> next)
    {
        var result = await resultTask;
        return result.IsSuccess 
            ? next(result.Value!) 
            : Result<TOut>.Fail(result.Error);
    }

    /// <summary>
    /// Railway operator: Chains async operations that return non-generic Result.
    /// </summary>
    public static async Task<Result> ThenAsync<TIn>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<Result>> next)
    {
        var result = await resultTask;
        return result.IsSuccess 
            ? await next(result.Value!) 
            : Result.Fail(result.Error);
    }

    /// <summary>
    /// Railway operator: Chains async operations from non-generic Result.
    /// </summary>
    public static async Task<Result<TOut>> ThenAsync<TOut>(
        this Task<Result> resultTask,
        Func<Task<Result<TOut>>> next)
    {
        var result = await resultTask;
        return result.IsSuccess 
            ? await next() 
            : Result<TOut>.Fail(result.Error);
    }

    /// <summary>
    /// Provides a fallback operation if the result fails.
    /// This is the "else" branch in Railway Oriented Programming.
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="resultTask">The source result task</param>
    /// <param name="fallback">Fallback operation that receives the error</param>
    /// <returns>Original result on success, fallback result on failure</returns>
    /// <example>
    /// await GetWorkflowAsync()
    ///     .OrElseAsync(error => GetDefaultWorkflowAsync());
    /// </example>
    public static async Task<Result<T>> OrElseAsync<T>(
        this Task<Result<T>> resultTask,
        Func<Error, Task<Result<T>>> fallback)
    {
        var result = await resultTask;
        return result.IsSuccess 
            ? result 
            : await fallback(result.Error);
    }

    /// <summary>
    /// Compensates for a failure by providing an alternative value.
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="resultTask">The source result task</param>
    /// <param name="compensate">Function that provides alternative value based on error</param>
    /// <returns>Original result on success, compensated result on failure</returns>
    /// <example>
    /// await GetCachedDataAsync()
    ///     .CompensateAsync(error => GetFreshDataAsync());
    /// </example>
    public static async Task<Result<T>> CompensateAsync<T>(
        this Task<Result<T>> resultTask,
        Func<Error, Task<T>> compensate)
    {
        var result = await resultTask;
        if (result.IsSuccess) return result;
        
        var compensatedValue = await compensate(result.Error);
        return Result<T>.Ok(compensatedValue);
    }

    /// <summary>
    /// Executes a side effect on success without altering the result.
    /// Useful for logging, notifications, metrics, etc.
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="resultTask">The source result task</param>
    /// <param name="onSuccess">Action to perform on success</param>
    /// <returns>The original result</returns>
    /// <example>
    /// await CreateInstanceAsync()
    ///     .OnSuccessAsync(instance => logger.LogInformation("Created: {Id}", instance.Id))
    ///     .ThenAsync(instance => ExecuteTransitionAsync(instance));
    /// </example>
    public static async Task<Result<T>> OnSuccessAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, Task> onSuccess)
    {
        var result = await resultTask;
        if (result.IsSuccess) 
            await onSuccess(result.Value!);
        return result;
    }

    /// <summary>
    /// Executes a side effect on failure without altering the error.
    /// Useful for error logging, alerting, etc.
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="resultTask">The source result task</param>
    /// <param name="onFailure">Action to perform on failure</param>
    /// <returns>The original result</returns>
    /// <example>
    /// await CreateInstanceAsync()
    ///     .OnFailureAsync(error => logger.LogError("Failed: {Code}", error.Code))
    ///     .OrElseAsync(error => CreateFallbackInstanceAsync());
    /// </example>
    public static async Task<Result<T>> OnFailureAsync<T>(
        this Task<Result<T>> resultTask,
        Func<Error, Task> onFailure)
    {
        var result = await resultTask;
        if (!result.IsSuccess) 
            await onFailure(result.Error);
        return result;
    }

    /// <summary>
    /// Synchronous version of OnSuccess for Task-wrapped results.
    /// </summary>
    public static async Task<Result<T>> OnSuccess<T>(
        this Task<Result<T>> resultTask,
        Action<T> onSuccess)
    {
        var result = await resultTask;
        if (result.IsSuccess) 
            onSuccess(result.Value!);
        return result;
    }

    /// <summary>
    /// Synchronous version of OnFailure for Task-wrapped results.
    /// </summary>
    public static async Task<Result<T>> OnFailure<T>(
        this Task<Result<T>> resultTask,
        Action<Error> onFailure)
    {
        var result = await resultTask;
        if (!result.IsSuccess) 
            onFailure(result.Error);
        return result;
    }

    #endregion

    #region Implicit Conversions & Helpers

    /// <summary>
    /// Converts a Result to a different type Result by failing with the same error.
    /// Useful for propagating errors across type boundaries.
    /// </summary>
    /// <typeparam name="T">Target type</typeparam>
    /// <param name="result">Source result</param>
    /// <returns>Failed result with the same error</returns>
    public static Result<T> ToResult<T>(this Result result)
        => result.IsSuccess 
            ? throw new InvalidOperationException("Cannot convert successful Result to Result<T> without a value")
            : Result<T>.Fail(result.Error);

    /// <summary>
    /// Converts a Result&lt;T&gt; to Result&lt;TOut&gt; by failing with the same error.
    /// </summary>
    public static Result<TOut> ToResult<TIn, TOut>(this Result<TIn> result)
        => result.IsSuccess 
            ? throw new InvalidOperationException("Cannot convert successful Result<T> to Result<TOut> without mapping")
            : Result<TOut>.Fail(result.Error);

    /// <summary>
    /// Unwraps the result, throwing an exception if failed.
    /// Use with caution - defeats the purpose of Result pattern.
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="result">The result to unwrap</param>
    /// <returns>The value if successful</returns>
    /// <exception cref="InvalidOperationException">If result is failed</exception>
    public static T Unwrap<T>(this Result<T> result)
        => result.IsSuccess 
            ? result.Value! 
            : throw new InvalidOperationException($"Result unwrap failed: {result.Error.Code} - {result.Error.Message}");

    /// <summary>
    /// Gets the value or a default if failed.
    /// </summary>
    public static T? ValueOrDefault<T>(this Result<T> result, T? defaultValue = default)
        => result.IsSuccess ? result.Value : defaultValue;

    /// <summary>
    /// Gets the value or executes a factory function if failed.
    /// </summary>
    public static T ValueOr<T>(this Result<T> result, Func<T> factory)
        => result.IsSuccess ? result.Value! : factory();

    /// <summary>
    /// Gets the value or executes an async factory function if failed.
    /// </summary>
    public static async Task<T> ValueOrAsync<T>(this Task<Result<T>> resultTask, Func<Task<T>> factory)
    {
        var result = await resultTask;
        return result.IsSuccess ? result.Value! : await factory();
    }

    #endregion

    #region Conversion Helpers

    /// <summary>
    /// Converts a nullable value to a Result.
    /// </summary>
    public static Result<T> ToResult<T>(this T? value, Error error) where T : class
        => value is not null ? Result<T>.Ok(value) : Result<T>.Fail(error);

    /// <summary>
    /// Converts a nullable struct to a Result.
    /// </summary>
    public static Result<T> ToResult<T>(this T? value, Error error) where T : struct
        => value.HasValue ? Result<T>.Ok(value.Value) : Result<T>.Fail(error);

    #endregion
}

