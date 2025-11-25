using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
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
    public async Task<Result> ExecuteAsync(
        TriggerTransitionTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                _logger.LogInformation("Handling Start trigger for task {TaskKey} - Domain: {Domain}, Flow: {Flow}",
                    task.Key, task.TriggerDomain, task.TriggerFlow);

                // Create path using InstanceUrlTemplates.Start format
                var path = string.Format(InstanceUrlTemplates.Start,
                    task.TriggerDomain,
                    task.TriggerFlow);
                
                var httpTaskResult = _httpTaskFactory.CreateHttpTask(task, context, path, "POST");
                if (!httpTaskResult.IsSuccess)
                {
                    _logger.LogError("Failed to create HTTP task for Start trigger: {Error}", httpTaskResult.Error.Code);
                    throw new InvalidOperationException($"Failed to create HTTP task: {httpTaskResult.Error.Message}");
                }

                var httpTask = httpTaskResult.Value!;

                var httpExecutor = _taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

                if (httpExecutor == null)
                    throw new InvalidOperationException("HttpTaskExecutor not found");

                _logger.LogDebug("Calling HttpTaskExecutor.CallAsync for Start trigger task {TaskKey}", task.Key);
                await httpExecutor.CallAsync(httpTask, context, ct);
            },
            cancellationToken,
            ex => Error.Failure(WorkflowErrorCodes.TriggerStartExecutionFailed, 
                $"Failed to execute Start trigger for task '{task.Key}': {ex.Message}"));
    }
}

