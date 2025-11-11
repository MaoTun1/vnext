using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.TriggerTransition;

/// <summary>
/// Strategy for handling GetInstanceData trigger type.
/// Retrieves instance data from a workflow instance using the data function endpoint.
/// </summary>
public sealed class GetInstanceDataTriggerStrategy : ITriggerTransitionStrategy
{
    private readonly ITaskExecutorFactory _taskExecutorFactory;
    private readonly ITriggerTransitionHttpTaskFactory _httpTaskFactory;
    private readonly ILogger<GetInstanceDataTriggerStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetInstanceDataTriggerStrategy"/> class.
    /// </summary>
    /// <param name="taskExecutorFactory">Factory for creating task executors.</param>
    /// <param name="httpTaskFactory">Factory for creating HTTP tasks for trigger transitions.</param>
    /// <param name="logger">The logger instance.</param>
    public GetInstanceDataTriggerStrategy(
        ITaskExecutorFactory taskExecutorFactory,
        ITriggerTransitionHttpTaskFactory httpTaskFactory,
        ILogger<GetInstanceDataTriggerStrategy> logger)
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
        _logger.LogInformation("Handling GetInstanceData trigger for task {TaskKey} - Domain: {Domain}, Flow: {Flow}, InstanceId: {InstanceId}",
            task.Key, task.TransitionDomain, task.TransitionFlow, context.Instance.Id);

        // Build path with or without extensions
        string path;
        if (task.Extensions != null && task.Extensions.Length > 0)
        {
            var extensionsParam = string.Join(",", task.Extensions);
            path = string.Format(InstanceUrlTemplates.DataWithExtensions,
                task.TransitionDomain,
                task.TransitionFlow,
                context.Instance.Id,
                extensionsParam);
        }
        else
        {
            path = string.Format(InstanceUrlTemplates.Data,
                task.TransitionDomain,
                task.TransitionFlow,
                context.Instance.Id);
        }

        var httpTask = _httpTaskFactory.CreateHttpTask(task, context, path, "GET");

        var httpExecutor = _taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

        if (httpExecutor == null)
            throw new InvalidOperationException("HttpTaskExecutor not found");

        _logger.LogDebug("Calling HttpTaskExecutor.CallAsync for GetInstanceData trigger task {TaskKey}", task.Key);
        await httpExecutor.CallAsync(httpTask, context, cancellationToken);
    }
}