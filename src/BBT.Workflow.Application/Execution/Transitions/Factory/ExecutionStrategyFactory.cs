using BBT.Workflow.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Factory implementation for creating appropriate execution strategies based on execution mode.
/// Uses dependency injection to resolve strategies.
/// </summary>
public sealed class ExecutionStrategyFactory(
    IServiceProvider serviceProvider,
    ILogger<ExecutionStrategyFactory> logger) : IExecutionStrategyFactory
{
    /// <inheritdoc />
    public Result<ITransitionStrategy> Get(ExecMode mode)
    {
        logger.LogDebug("Resolving execution strategy for mode {ExecMode}", mode);

        ITransitionStrategy? strategy = mode switch
        {
            ExecMode.Sync => serviceProvider.GetService<SyncTransitionStrategy>(),
            ExecMode.Async => serviceProvider.GetService<AsyncTransitionStrategy>(),
            _ => null
        };

        if (strategy == null)
        {
            logger.LogError("No execution strategy found for mode {ExecMode}", mode);
            return Result<ITransitionStrategy>.Fail(
                Error.Failure("strategy.notFound", $"No execution strategy found for mode {mode}", mode.ToString()));
        }

        logger.LogDebug("Resolved execution strategy {StrategyType} for mode {ExecMode}",
            strategy.GetType().Name, mode);

        return Result<ITransitionStrategy>.Ok(strategy);
    }
}
