using BBT.Aether.Results;
using Polly;

namespace BBT.Workflow.Resilience;

/// <summary>
/// Factory for creating resilience pipelines that work with Result&lt;T&gt; pattern.
/// Enables retry logic for transient failures based on error codes.
/// </summary>
public interface IResultResiliencePipelineFactory
{
    /// <summary>
    /// Creates a resilience pipeline for Result&lt;T&gt; operations.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="operationName">Name of the operation for logging/metrics.</param>
    /// <param name="overrideOptions">Optional override options.</param>
    /// <returns>A resilience pipeline that handles Result&lt;T&gt; retry logic.</returns>
    ResiliencePipeline<Result<T>> CreatePipeline<T>(
        string operationName,
        ResultRetryOptions? overrideOptions = null);

    /// <summary>
    /// Creates a resilience pipeline for Result (non-generic) operations.
    /// </summary>
    /// <param name="operationName">Name of the operation for logging/metrics.</param>
    /// <param name="overrideOptions">Optional override options.</param>
    /// <returns>A resilience pipeline that handles Result retry logic.</returns>
    ResiliencePipeline<Result> CreatePipeline(
        string operationName,
        ResultRetryOptions? overrideOptions = null);
}

