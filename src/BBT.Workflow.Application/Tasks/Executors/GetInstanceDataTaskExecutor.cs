using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes get instance data tasks that retrieve instance data from workflow instances.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="taskExecutorFactory">Factory for creating task executors.</param>
/// <param name="httpTaskFactory">Factory for creating HTTP tasks.</param>
/// <param name="logger">The logger instance for logging get instance data task execution details.</param>
public sealed class GetInstanceDataTaskExecutor(
    IScriptEngine scriptEngine,
    ITaskExecutorFactory taskExecutorFactory,
    ITriggerTransitionHttpTaskFactory httpTaskFactory,
    ILogger<GetInstanceDataTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a get instance data task by preparing the request through script mapping,
    /// creating an HTTP task to retrieve instance data, and processing the response.
    /// </summary>
    /// <param name="task">The get instance data workflow task containing retrieval configuration.</param>
    /// <param name="scriptCode">The script code that handles input preparation and output processing.</param>
    /// <param name="context">The script context containing instance data and headers.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed response data
    /// from the data retrieval after output mapping transformation.
    /// </returns>
    /// <exception cref="InvalidCastException">Thrown when the task cannot be cast to GetInstanceDataTask type.</exception>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var getDataTask = (task as GetInstanceDataTask)!;
        
        try
        {
            await PrepareInputAsync(getDataTask, scriptCode, context, cancellationToken);

            // Execute get instance data logic
            var executionResult = await ExecuteGetInstanceDataAsync(getDataTask, context, cancellationToken);
            
            // If execution failed, throw exception to maintain existing error handling behavior
            if (!executionResult.IsSuccess)
            {
                Logger.LogError("Get instance data execution failed for task {TaskKey}: {ErrorCode} - {ErrorMessage}",
                    getDataTask.Key, executionResult.Error.Code, executionResult.Error.Message);
                throw new InvalidOperationException($"Get instance data execution failed: {executionResult.Error.Message}");
            }
            
            var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);
            return outputResponse;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred during get instance data task {TaskKey} execution - Domain: {Domain}, Flow: {Flow}",
                getDataTask.Key, getDataTask.TriggerDomain, getDataTask.TriggerFlow);

            StandardTaskResponse standardResponse = CreateErrorResponse(
                errorMessage: ex.Message,
                taskType: nameof(TaskType.GetInstanceData),
                executionDurationMs: 0,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["TaskKey"] = getDataTask.Key,
                    ["Domain"] = getDataTask.TriggerDomain,
                    ["Flow"] = getDataTask.TriggerFlow
                });
            context.SetStandardResponse(standardResponse);
            throw;
        }
    }

    /// <summary>
    /// Executes the get instance data logic by retrieving data from a target instance.
    /// </summary>
    private async Task<Result> ExecuteGetInstanceDataAsync(
        GetInstanceDataTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                // Resolve instance ID using the factory's ResolveInstanceIdAsync method
                var instanceIdResult = await httpTaskFactory.ResolveInstanceIdAsync(task, context, ct);
                if (!instanceIdResult.IsSuccess)
                {
                    Logger.LogError("Failed to resolve instance ID for GetInstanceData trigger: {Error}", instanceIdResult.Error.Code);
                    throw new InvalidOperationException($"Failed to resolve instance ID: {instanceIdResult.Error.Message}");
                }

                var instanceId = instanceIdResult.Value!;

                // Build path with or without extensions
                string path;
                if (task.Extensions?.Length > 0)
                {
                    var extensionsParam = string.Join(",", task.Extensions);
                    path = InstanceUrlTemplates.DataWithExtensions(
                        task.TriggerDomain,
                        task.TriggerFlow,
                        instanceId,
                        extensionsParam);
                }
                else
                {
                    path = InstanceUrlTemplates.Data(
                        task.TriggerDomain,
                        task.TriggerFlow,
                        instanceId);
                }

                var httpTaskResult = httpTaskFactory.CreateHttpTask(task, context, path, "GET");
                if (!httpTaskResult.IsSuccess)
                {
                    Logger.LogError("Failed to create HTTP task for GetInstanceData trigger: {Error}", httpTaskResult.Error.Code);
                    throw new InvalidOperationException($"Failed to create HTTP task: {httpTaskResult.Error.Message}");
                }

                var httpTask = httpTaskResult.Value!;

                var httpExecutor = taskExecutorFactory.GetExecutor(TaskType.Http) as HttpTaskExecutor;

                if (httpExecutor == null)
                    throw new InvalidOperationException("HttpTaskExecutor not found");
                
                await httpExecutor.CallAsync(httpTask, context, ct);
            },
            cancellationToken,
            ex => Error.Failure(WorkflowErrorCodes.TriggerGetInstanceDataFailed, 
                $"Failed to execute GetInstanceData trigger for task '{task.Key}': {ex.Message}"));
    }
}

