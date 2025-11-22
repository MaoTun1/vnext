using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Tasks.TriggerTransition;

/// <summary>
/// Strategy for handling GetInstanceData trigger type.
/// Retrieves instance data from a workflow instance using the data function endpoint.
/// </summary>
public sealed class GetInstanceDataTriggerStrategy : ITriggerTransitionStrategy
{
    private readonly ITaskExecutorFactory _taskExecutorFactory;
    private readonly ITriggerTransitionHttpTaskFactory _httpTaskFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetInstanceDataTriggerStrategy"/> class.
    /// </summary>
    /// <param name="taskExecutorFactory">Factory for creating task executors.</param>
    /// <param name="httpTaskFactory">Factory for creating HTTP tasks for trigger transitions.</param>
    public GetInstanceDataTriggerStrategy(
        ITaskExecutorFactory taskExecutorFactory,
        ITriggerTransitionHttpTaskFactory httpTaskFactory)
    {
        _taskExecutorFactory = taskExecutorFactory;
        _httpTaskFactory = httpTaskFactory;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        TriggerTransitionTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        // Build path with or without extensions
        string path;
        if (task.Extensions?.Length > 0)
        {
            var extensionsParam = string.Join(",", task.Extensions);
            path = string.Format(InstanceUrlTemplates.DataWithExtensions,
                task.TriggerDomain,
                task.TriggerFlow,
                context.Instance.Id,
                extensionsParam);
        }
        else
        {
            path = string.Format(InstanceUrlTemplates.Data,
                task.TriggerDomain,
                task.TriggerFlow,
                context.Instance.Id);
        }

        var httpTask = _httpTaskFactory.CreateHttpTask(task, context, path, "GET");

        var httpExecutor = _taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

        if (httpExecutor == null)
            throw new InvalidOperationException("HttpTaskExecutor not found");
        
        await httpExecutor.CallAsync(httpTask, context, cancellationToken);
    }
}