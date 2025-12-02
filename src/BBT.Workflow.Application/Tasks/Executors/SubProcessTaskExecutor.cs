using System.Text.Json;
using BBT.Aether.Guids;
using BBT.Aether.Results;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using BBT.Workflow.SubFlow;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes subprocess tasks that trigger transitions on correlated SubFlow instances.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="subflowStarter">Service for starting SubFlow workflows.</param>
/// <param name="instanceRepository">InstanceRepository for update instance correlation</param>
/// <param name="guidGenerator">Generator for creating unique identifiers.</param>
/// <param name="logger">The logger instance for logging subprocess task execution details.</param>
public sealed class SubProcessTaskExecutor(
    IScriptEngine scriptEngine,
    ISubflowStarter subflowStarter,
    IInstanceRepository instanceRepository,
    IGuidGenerator guidGenerator,
    ILogger<SubProcessTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Executes a subprocess task by preparing the request through script mapping,
    /// creating a correlation and starting the subprocess workflow.
    /// </summary>
    /// <param name="task">The subprocess workflow task containing subprocess configuration.</param>
    /// <param name="scriptCode">The script code that handles input preparation and output processing.</param>
    /// <param name="context">The script context containing instance data and headers.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed response data
    /// from the subprocess execution after output mapping transformation.
    /// </returns>
    /// <exception cref="InvalidCastException">Thrown when the task cannot be cast to SubProcessTask type.</exception>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var subProcessTask = (task as SubProcessTask)!;
        StandardTaskResponse standardResponse;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await PrepareInputAsync(subProcessTask, scriptCode, context, cancellationToken);

            // Execute subprocess logic
            var executionResult = await ExecuteSubProcessAsync(subProcessTask, context, cancellationToken);
            stopwatch.Stop();
            // If execution failed, throw exception to maintain existing error handling behavior
            if (executionResult.IsSuccess)
            {
                standardResponse = CreateSuccessResponse(
                    data: executionResult.Value,
                    taskType: nameof(TaskType.SubProcess),
                    executionDurationMs: stopwatch.ElapsedMilliseconds,
                    statusCode: 200,
                    headers: executionResult.Value?.Headers,
                    metadata: new Dictionary<string, object>
                    {
                        ["TaskKey"] = subProcessTask.Key,
                        ["Domain"] = subProcessTask.TriggerDomain,
                        ["Key"] = subProcessTask.TriggerKey,
                        ["Version"] = subProcessTask.TriggerVersion ?? ""
                    });
            }
            else
            {
                standardResponse = CreateErrorResponse(
                    errorMessage: executionResult.Error.Message ?? "SubProcess execution failed",
                    taskType: nameof(TaskType.SubProcess),
                    executionDurationMs: stopwatch.ElapsedMilliseconds,
                    statusCode: 400,
                    metadata: new Dictionary<string, object>
                    {
                        ["TaskKey"] = subProcessTask.Key,
                        ["Domain"] = subProcessTask.TriggerDomain,
                        ["Key"] = subProcessTask.TriggerKey,
                        ["Version"] = subProcessTask.TriggerVersion ?? ""
                    });
            }

            context.SetStandardResponse(standardResponse);
            if (executionResult.IsSuccess)
            {
                context.SetBody(executionResult.Value);    
            }
            
            var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);
            return outputResponse;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex,
                "Error occurred during subprocess task {TaskKey} execution - Domain: {Domain}, Key: {Key}, Version: {Version}",
                subProcessTask.Key, subProcessTask.TriggerDomain, subProcessTask.TriggerKey,
                subProcessTask.TriggerVersion);
            throw;
        }
    }

    /// <summary>
    /// Executes the subprocess logic by first starting the subprocess workflow,
    /// then creating correlation on success using Railway pattern.
    /// </summary>
    /// <returns>Result containing ScriptResponse with subprocess execution data.</returns>
    private async Task<Result<ScriptResponse>> ExecuteSubProcessAsync(
        SubProcessTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        Guid subFlowInstanceId = guidGenerator.Create();
        Guid correlationId = guidGenerator.Create();

        // Create SubFlow reference for the subprocess
        var subFlowReference = new Reference(
            task.TriggerKey!,
            task.TriggerDomain,
            RuntimeSysSchemaInfo.Flows,
            task.TriggerVersion ?? string.Empty);

        Dictionary<string, string?> headersDict = ConvertHeadersToDictionary(context.Headers);

        var inputMappingResult = new ScriptResponse
        {
            Data = task.Body?.ToDynamic(),
            Headers = headersDict,
            Key = subFlowInstanceId.ToString()
        };

        // Create correlation object (will be persisted only on success)
        var correlation = InstanceCorrelation.Create(
            correlationId,
            context.Instance.Id,
            context.Instance.GetCurrentState,
            subFlowInstanceId,
            SubFlowType.SubProcess.Code,
            task.TriggerDomain,
            task.TriggerKey!,
            task.TriggerVersion);

        // Railway pattern: Start SubProcess first, then create correlation on success
        return await ResultExtensions.TryAsync(
                async ct => await subflowStarter.SubStartAsync(
                    context.Workflow,
                    context.Instance,
                    subFlowReference,
                    context.Transition!,
                    correlation,
                    SubFlowType.SubProcess.Code,
                    inputMappingResult,
                    ct),
                cancellationToken,
                ex => Error.Failure(WorkflowErrorCodes.TriggerSubProcessExecutionFailed,
                    $"Failed to start SubProcess for task '{task.Key}': {ex.Message}"))
            .ThenAsync(async () =>
            {
                // SubProcess started successfully, now persist the correlation
                return await ResultExtensions.TryAsync(
                    async ct =>
                    {
                        var trackedInstance = await instanceRepository.GetAsync(context.Instance.Id, true, ct);
                        trackedInstance.AddCorrelation(correlation);
                        await instanceRepository.UpdateAsync(trackedInstance, true, ct);
                        return inputMappingResult;
                    },
                    cancellationToken,
                    ex => Error.Failure(WorkflowErrorCodes.TriggerSubProcessExecutionFailed,
                        $"Failed to create correlation for task '{task.Key}': {ex.Message}"));
            });
    }

    /// <summary>
    /// Converts dynamic Headers to Dictionary&lt;string, string?&gt; using Result pattern.
    /// Handles ExpandoObject, Dictionary, and other dynamic types.
    /// </summary>
    /// <param name="dynamicHeaders">The dynamic headers object to convert.</param>
    /// <returns>A Result containing Dictionary&lt;string, string?&gt; or an error.</returns>
    private static Dictionary<string, string?> ConvertHeadersToDictionary(
        dynamic? dynamicHeaders)
    {
        if (dynamicHeaders == null)
            return new Dictionary<string, string?>();

        if (dynamicHeaders is Dictionary<string, string?> dict)
            return dict;

        if (dynamicHeaders is IDictionary<string, object?> expandoDict)
        {
            return expandoDict.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToString()
            );
        }

        return new Dictionary<string, string?>();
    }
}