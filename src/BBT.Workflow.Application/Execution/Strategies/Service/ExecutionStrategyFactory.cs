using Microsoft.Extensions.DependencyInjection;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Factory implementation for creating execution strategies based on execution mode.
/// Uses dependency injection to resolve strategy instances.
/// </summary>
public sealed class ExecutionStrategyFactory(IServiceProvider serviceProvider) : IExecutionStrategyFactory
{
    /// <inheritdoc />
    public IInstanceStartStrategy GetInstanceStartStrategy(bool isSync)
    {
        var strategies = serviceProvider.GetServices<IInstanceStartStrategy>();
        var strategy = strategies.FirstOrDefault(s => s.CanHandle(isSync));

        return strategy ?? throw new NotSupportedException(
            $"No instance start strategy found for execution mode: {(isSync ? "Sync" : "Async")}");
    }

    /// <inheritdoc />
    public ITransitionStrategy GetTransitionStrategy(bool isSync)
    {
        var strategies = serviceProvider.GetServices<ITransitionStrategy>();
        var strategy = strategies.FirstOrDefault(s => s.CanHandle(isSync));

        return strategy ?? throw new NotSupportedException(
            $"No transition strategy found for execution mode: {(isSync ? "Sync" : "Async")}");
    }
}
