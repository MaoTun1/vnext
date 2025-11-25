using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
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
    public async Task<Result> ExecuteAsync(
        TriggerTransitionTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                _logger.LogInformation("Handling GetInstanceData trigger for task {TaskKey} - Domain: {Domain}, Flow: {Flow}, InstanceId: {InstanceId}",
                    task.Key, task.TriggerDomain, task.TriggerFlow, context.Instance.Id);

                // Resolve instance ID using the factory's ResolveInstanceIdAsync method
                var instanceIdResult = await _httpTaskFactory.ResolveInstanceIdAsync(task, context, ct);
                if (!instanceIdResult.IsSuccess)
                {
                    _logger.LogError("Failed to resolve instance ID for GetInstanceData trigger: {Error}", instanceIdResult.Error.Code);
                    throw new InvalidOperationException($"Failed to resolve instance ID: {instanceIdResult.Error.Message}");
                }

                var instanceId = instanceIdResult.Value!;

                // Build path with or without extensions
                string path;
                if (task.Extensions?.Length > 0)
                {
                    var extensionsParam = string.Join(",", task.Extensions);
                    path = string.Format(InstanceUrlTemplates.DataWithExtensions,
                        task.TriggerDomain,
                        task.TriggerFlow,
                        instanceId,
                        extensionsParam);
                }
                else
                {
                    path = string.Format(InstanceUrlTemplates.Data,
                        task.TriggerDomain,
                        task.TriggerFlow,
                        instanceId);
                }

                var httpTaskResult = _httpTaskFactory.CreateHttpTask(task, context, path, "GET");
                if (!httpTaskResult.IsSuccess)
                {
                    _logger.LogError("Failed to create HTTP task for GetInstanceData trigger: {Error}", httpTaskResult.Error.Code);
                    throw new InvalidOperationException($"Failed to create HTTP task: {httpTaskResult.Error.Message}");
                }

                var httpTask = httpTaskResult.Value!;

                var httpExecutor = _taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

                if (httpExecutor == null)
                    throw new InvalidOperationException("HttpTaskExecutor not found");

                _logger.LogDebug("Calling HttpTaskExecutor.CallAsync for GetInstanceData trigger task {TaskKey}", task.Key);
                await httpExecutor.CallAsync(httpTask, context, ct);
            },
            cancellationToken,
            ex => Error.Failure(WorkflowErrorCodes.TriggerGetInstanceDataFailed, 
                $"Failed to execute GetInstanceData trigger for task '{task.Key}': {ex.Message}"));
    }
}