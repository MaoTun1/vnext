using System.Text.Json;
using BBT.Aether.Guids;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
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
/// <param name="runtimeInfoProvider">The runtime information provider for domain checks.</param>
/// <param name="subflowStarter">Service for starting SubFlow workflows.</param>
/// <param name="guidGenerator">Generator for creating unique identifiers.</param>
/// <param name="logger">The logger instance for logging subprocess task execution details.</param>
public sealed class SubProcessTaskExecutor(
    IScriptEngine scriptEngine,
    IRuntimeInfoProvider runtimeInfoProvider,
    ISubflowStarter subflowStarter,
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

        Logger.LogInformation("Starting subprocess task execution for task {TaskKey} - Domain: {Domain}, Key: {Key}, Version: {Version}",
            subProcessTask.Key, subProcessTask.TriggerDomain, subProcessTask.TriggerKey, subProcessTask.TriggerVersion);

        try
        {
            // Check runtime domain
            runtimeInfoProvider.Check(context.Runtime.Domain);

            Logger.LogDebug("Preparing input for subprocess task {TaskKey}", subProcessTask.Key);
            await PrepareInputAsync(subProcessTask, scriptCode, context, cancellationToken);

            // Execute subprocess logic
            var executionResult = await ExecuteSubProcessAsync(subProcessTask, context, cancellationToken);
            
            // If execution failed, throw exception to maintain existing error handling behavior
            if (!executionResult.IsSuccess)
            {
                Logger.LogError("SubProcess execution failed for task {TaskKey}: {ErrorCode} - {ErrorMessage}",
                    subProcessTask.Key, executionResult.Error.Code, executionResult.Error.Message);
                throw new InvalidOperationException($"SubProcess execution failed: {executionResult.Error.Message}");
            }

            Logger.LogDebug("Processing output for subprocess task {TaskKey}", subProcessTask.Key);
            var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);

            Logger.LogInformation("SubProcess task {TaskKey} execution completed, returning processed output", subProcessTask.Key);
            return outputResponse;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred during subprocess task {TaskKey} execution - Domain: {Domain}, Key: {Key}, Version: {Version}",
                subProcessTask.Key, subProcessTask.TriggerDomain, subProcessTask.TriggerKey, subProcessTask.TriggerVersion);

            StandardTaskResponse standardResponse = CreateErrorResponse(
                errorMessage: ex.Message,
                taskType: nameof(TaskType.SubProcess),
                executionDurationMs: 0,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["TaskKey"] = subProcessTask.Key,
                    ["Domain"] = subProcessTask.TriggerDomain,
                    ["Key"] = subProcessTask.TriggerKey,
                    ["Version"] = subProcessTask.TriggerVersion ?? ""
                });
            context.SetStandardResponse(standardResponse);
            throw;
        }
    }

    /// <summary>
    /// Executes the subprocess logic by creating a correlation and starting the subprocess workflow.
    /// </summary>
    private async Task<Result> ExecuteSubProcessAsync(
        SubProcessTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                Logger.LogInformation(
                    "Executing SubProcess trigger for task {TaskKey} - Domain: {Domain}, Key: {Key}, Version: {Version}",
                    task.Key, task.TriggerDomain, task.TriggerKey, task.TriggerVersion);

                // Create correlation for SubProcess
                var correlation = InstanceCorrelation.Create(
                    guidGenerator.Create(),
                    context.Instance.Id,
                    context.Instance.GetCurrentState,
                    guidGenerator.Create(),
                    SubFlowType.SubProcess.Code,
                    task.TriggerDomain,
                    task.TriggerKey!,
                    task.TriggerVersion);
                context.Instance.AddCorrelation(correlation);

                // Create a SubFlow reference for the subprocess
                var subFlowReference = new Reference(
                    task.TriggerKey!,
                    task.TriggerDomain,
                    RuntimeSysSchemaInfo.Flows,
                    task.TriggerVersion ?? string.Empty);

                // Prepare input mapping result with task data
                // Convert JsonElement to a normal object so it can be serialized again in SubflowStarter
                // object? bodyData = null;
                // if (task.Body.HasValue)
                // {
                //     bodyData = JsonSerializer.Deserialize<object>(task.Body.Value.GetRawText());
                // }

                // Convert dynamic Headers to Dictionary<string, string?>
                var headersResult = ConvertHeadersToDictionary(context.Headers, task.Key);
                Dictionary<string, string?> headersDict;

                if (!headersResult.IsSuccess)
                {
                    Logger.LogWarning(
                        "Failed to convert headers for task {TaskKey}: {ErrorMessage}. Using empty dictionary.",
                        (object)task.Key, (object?)headersResult.Error.Message);
                    headersDict = new Dictionary<string, string?>();
                }
                else
                {
                    headersDict = headersResult.Value;
                }

                var inputMappingResult = new ScriptResponse
                {
                    Data = task.Body,
                    Headers = headersDict,
                    Key = Guid.NewGuid().ToString()
                };

                // Start the SubProcess using simplified SubStartAsync method
                await subflowStarter.SubStartAsync(
                    context.Workflow,
                    context.Instance,
                    subFlowReference,
                    context.Transition!,
                    correlation,
                    SubFlowType.SubProcess.Code,
                    inputMappingResult,
                    ct);

                Logger.LogInformation(
                    "SubProcess trigger completed for task {TaskKey} with correlation {CorrelationId}",
                    task.Key, correlation.Id);
            },
            cancellationToken,
            ex => Error.Failure(WorkflowErrorCodes.TriggerSubProcessExecutionFailed, 
                $"Failed to execute SubProcess trigger for task '{task.Key}': {ex.Message}"));
    }

    /// <summary>
    /// Converts dynamic Headers to Dictionary&lt;string, string?&gt; using Result pattern.
    /// Handles ExpandoObject, Dictionary, and other dynamic types.
    /// </summary>
    /// <param name="dynamicHeaders">The dynamic headers object to convert.</param>
    /// <param name="taskKey">The task key for error reporting.</param>
    /// <returns>A Result containing Dictionary&lt;string, string?&gt; or an error.</returns>
    private static Result<Dictionary<string, string?>> ConvertHeadersToDictionary(
        dynamic? dynamicHeaders,
        string taskKey)
    {
        if (dynamicHeaders == null)
            return Result<Dictionary<string, string?>>.Ok(new Dictionary<string, string?>());

        if (dynamicHeaders is Dictionary<string, string?> dict)
            return Result<Dictionary<string, string?>>.Ok(dict);

        if (dynamicHeaders is IDictionary<string, object?> expandoDict)
        {
            return ResultExtensions.Try<Dictionary<string, string?>>(
                () => expandoDict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToString()),
                ex => WorkflowErrors.TaskHeadersConversionFailed(taskKey, ex.Message));
        }

        return ResultExtensions.Try<Dictionary<string, string?>>(
            () =>
            {
                var json = JsonSerializer.Serialize(dynamicHeaders);
                var result = JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
                return result ?? new Dictionary<string, string?>();
            },
            ex => WorkflowErrors.TaskHeadersConversionFailed(taskKey, ex.Message));
    }
}

