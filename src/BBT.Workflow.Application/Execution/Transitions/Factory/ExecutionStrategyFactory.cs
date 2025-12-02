using BBT.Aether.Results;
using Microsoft.Extensions.DependencyInjection;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Factory implementation for creating appropriate execution strategies based on execution mode.
/// Uses dependency injection to resolve strategies.
/// </summary>
public sealed class ExecutionStrategyFactory(
    IServiceProvider serviceProvider) : IExecutionStrategyFactory
{
    /// <inheritdoc />
    public Result<ITransitionStrategy> Get(ExecMode mode)
    {
        ITransitionStrategy? strategy = mode switch
        {
            ExecMode.Sync => serviceProvider.GetService<SyncTransitionStrategy>(),
            ExecMode.Async => serviceProvider.GetService<AsyncTransitionStrategy>(),
            ExecMode.Resume => serviceProvider.GetService<SyncTransitionStrategy>(), // Resume mode uses Sync strategy
            _ => null
        };

        return strategy != null
            ? Result<ITransitionStrategy>.Ok(strategy)
            : Result<ITransitionStrategy>.Fail(
                Error.NotSupported(
                    WorkflowErrorCodes.ExecutionStrategyNotSupported,
                    $"No execution strategy found for mode {mode}"
                )
            );
    }
}