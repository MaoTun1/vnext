using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.TriggerTransition;

/// <summary>
/// Strategy for handling Trigger (Direct) trigger type.
/// Executes a transition on the current workflow instance using HttpTask to call the transition endpoint.
/// </summary>
public sealed class DirectTriggerStrategy : ITriggerTransitionStrategy
{
    private readonly ITaskExecutorFactory _taskExecutorFactory;
    private readonly ITriggerTransitionHttpTaskFactory _httpTaskFactory;
    private readonly ILogger<DirectTriggerStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectTriggerStrategy"/> class.
    /// </summary>
    /// <param name="taskExecutorFactory">Factory for creating task executors.</param>
    /// <param name="httpTaskFactory">Factory for creating HTTP tasks for trigger transitions.</param>
    /// <param name="logger">The logger instance.</param>
    public DirectTriggerStrategy(
        ITaskExecutorFactory taskExecutorFactory,
        ITriggerTransitionHttpTaskFactory httpTaskFactory,
        ILogger<DirectTriggerStrategy> logger)
    {
        _taskExecutorFactory = taskExecutorFactory;
        _httpTaskFactory = httpTaskFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        TriggerTransitionTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(task.TransitionName))
            throw new InvalidOperationException("TransitionName is required for Trigger (Direct) trigger type");

        _logger.LogInformation("Handling Direct trigger for task {TaskKey} - InstanceId: {InstanceId}, Transition: {Transition}",
            task.Key, context.Instance.Id, task.TransitionName);

        // Resolve instance ID using the factory's ResolveInstanceIdAsync method
        var instanceId = await _httpTaskFactory.ResolveInstanceIdAsync(task, context, cancellationToken);

        // Create path using InstanceUrlTemplates.Transition format
        var path = string.Format(InstanceUrlTemplates.Transition,
            task.TriggerDomain,
            task.TriggerFlow,
            instanceId,
            task.TransitionName);

        var httpTask = _httpTaskFactory.CreateHttpTask(task, context, path, "PATCH");

        var httpExecutor = _taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

        if (httpExecutor == null)
            throw new InvalidOperationException("HttpTaskExecutor not found");

        _logger.LogDebug("Calling HttpTaskExecutor.CallAsync for Direct trigger task {TaskKey}", task.Key);
        await httpExecutor.CallAsync(httpTask, context, cancellationToken);
    }
}

