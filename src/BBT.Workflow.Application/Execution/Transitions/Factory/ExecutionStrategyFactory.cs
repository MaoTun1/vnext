using BBT.Aether.Results;

namespace BBT.Workflow.Execution.Strategies;

/// <summary>
/// Factory implementation for creating appropriate execution strategies based on execution mode.
/// Uses dependency injection to resolve strategies.
/// Falls back to SyncTransitionStrategy if no strategy is found for the given mode.
/// </summary>
public sealed class ExecutionStrategyFactory(
    IEnumerable<ITransitionStrategy> strategies) : IExecutionStrategyFactory
{
    private readonly IEnumerable<ITransitionStrategy> _strategies = strategies;

    /// <inheritdoc />
    public Result<ITransitionStrategy> Get(ExecMode mode)
    {
        // Try to find strategy for the requested mode
        var strategy = _strategies.FirstOrDefault(s => s.Mode == mode);
        if (strategy != null)
        {
            return Result<ITransitionStrategy>.Ok(strategy);
        }

        // Fallback to Sync strategy as default
        var defaultStrategy = _strategies.FirstOrDefault(s => s.Mode == ExecMode.Sync);
        if (defaultStrategy != null)
        {
            return Result<ITransitionStrategy>.Ok(defaultStrategy);
        }

        // This should never happen if SyncTransitionStrategy is registered
        return Result<ITransitionStrategy>.Fail(
            Error.NotSupported(
                WorkflowErrorCodes.ExecutionStrategyNotSupported,
                $"No execution strategy found for mode {mode} and no default Sync strategy available"
            )
        );
    }
}
