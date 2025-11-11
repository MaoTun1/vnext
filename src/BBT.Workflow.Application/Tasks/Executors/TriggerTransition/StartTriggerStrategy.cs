using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.TriggerTransition;

/// <summary>
/// Strategy for handling Start trigger type.
/// Creates a new workflow instance using HttpTask to call the workflow start endpoint.
/// </summary>
public sealed class StartTriggerStrategy : ITriggerTransitionStrategy
{
    private readonly ITaskExecutorFactory _taskExecutorFactory;
    private readonly ITriggerTransitionHttpTaskFactory _httpTaskFactory;
    private readonly ILogger<StartTriggerStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartTriggerStrategy"/> class.
    /// </summary>
    /// <param name="taskExecutorFactory">Factory for creating task executors.</param>
    /// <param name="httpTaskFactory">Factory for creating HTTP tasks for trigger transitions.</param>
    /// <param name="logger">The logger instance.</param>
    public StartTriggerStrategy(
        ITaskExecutorFactory taskExecutorFactory,
        ITriggerTransitionHttpTaskFactory httpTaskFactory,
        ILogger<StartTriggerStrategy> logger)
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
        _logger.LogInformation("Handling Start trigger for task {TaskKey} - Domain: {Domain}, Flow: {Flow}",
            task.Key, task.TransitionDomain, task.TransitionFlow);

        // Create path using format: /{domain}/workflows/{workflow}/instances/start
        var path = string.Format("/{0}/workflows/{1}/instances/start",
            task.TransitionDomain,
            task.TransitionFlow);

        var httpTask = _httpTaskFactory.CreateHttpTask(task, context, path, "POST");

        var httpExecutor = _taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

        if (httpExecutor == null)
            throw new InvalidOperationException("HttpTaskExecutor not found");

        _logger.LogDebug("Calling HttpTaskExecutor.CallAsync for Start trigger task {TaskKey}", task.Key);
        await httpExecutor.CallAsync(httpTask, context, cancellationToken);
    }
}

