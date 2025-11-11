using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.TriggerTransition;

/// <summary>
/// Factory implementation for creating appropriate trigger transition strategies based on trigger type.
/// Uses dependency injection to resolve strategies.
/// </summary>
public sealed class TriggerTransitionStrategyFactory : ITriggerTransitionStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TriggerTransitionStrategyFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerTransitionStrategyFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve strategy instances.</param>
    /// <param name="logger">The logger instance.</param>
    public TriggerTransitionStrategyFactory(
        IServiceProvider serviceProvider,
        ILogger<TriggerTransitionStrategyFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public ITriggerTransitionStrategy Get(TriggerTransitionType type)
    {
        _logger.LogDebug("Resolving trigger transition strategy for type {TriggerType}", type);

        ITriggerTransitionStrategy? strategy = type switch
        {
            TriggerTransitionType.Start => _serviceProvider.GetService<StartTriggerStrategy>(),
            TriggerTransitionType.Trigger => _serviceProvider.GetService<DirectTriggerStrategy>(),
            TriggerTransitionType.SubProcess => _serviceProvider.GetService<SubProcessTriggerStrategy>(),
            _ => null
        };

        if (strategy == null)
        {
            _logger.LogError("No trigger transition strategy found for type {TriggerType}", type);
            throw new NotSupportedException($"No trigger transition strategy found for type {type}");
        }

        _logger.LogDebug("Resolved trigger transition strategy {StrategyType} for type {TriggerType}",
            strategy.GetType().Name, type);

        return strategy;
    }
}

