using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.TriggerTransition;

/// <summary>
/// Strategy for handling SubProcess trigger type.
/// This is a placeholder for future implementation that will trigger transitions on correlated SubFlow instances.
/// </summary>
public sealed class SubProcessTriggerStrategy : ITriggerTransitionStrategy
{
    private readonly ILogger<SubProcessTriggerStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubProcessTriggerStrategy"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public SubProcessTriggerStrategy(ILogger<SubProcessTriggerStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(
        TriggerTransitionTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogError("SubProcess trigger type is not yet implemented for task {TaskKey}", task.Key);
        throw new NotImplementedException(
            $"SubProcess trigger type is not yet implemented. Task: {task.Key}");
    }
}

