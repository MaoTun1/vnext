using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Tasks.TriggerTransition;

/// <summary>
/// Strategy for handling Start trigger type.
/// Creates a new workflow instance using HttpTask to call the workflow start endpoint.
/// </summary>
public sealed class StartTriggerStrategy : ITriggerTransitionStrategy
{
    private readonly ITaskExecutorFactory _taskExecutorFactory;
    private readonly ITriggerTransitionHttpTaskFactory _httpTaskFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartTriggerStrategy"/> class.
    /// </summary>
    /// <param name="taskExecutorFactory">Factory for creating task executors.</param>
    /// <param name="httpTaskFactory">Factory for creating HTTP tasks for trigger transitions.</param>
    public StartTriggerStrategy(
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
        // Create path using InstanceUrlTemplates.Start format
        var path = string.Format(InstanceUrlTemplates.Start,
            task.TriggerDomain,
            task.TriggerFlow);
        
        var httpTask = _httpTaskFactory.CreateHttpTask(task, context, path, "POST");

        var httpExecutor = _taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

        if (httpExecutor == null)
            throw new InvalidOperationException("HttpTaskExecutor not found");
        
        await httpExecutor.CallAsync(httpTask, context, cancellationToken);
    }
}

