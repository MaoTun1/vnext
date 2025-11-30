using System.Text;
using System.Text.Json;
using BBT.Aether.Application.Services;
using BBT.Aether.Results;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;

namespace BBT.Workflow.Functions;

/// <summary>
/// Application service for function operations using Railway pattern.
/// </summary>
public sealed class FunctionAppService(
    IServiceProvider serviceProvider,
    IRuntimeInfoProvider runtimeInfoProvider,
    IInstanceRepository instanceRepository,
    IScriptContextFactory scriptContextFactory,
    IComponentCacheStore componentCacheStore,
    ITaskOrchestrationService taskExecutionService) : ApplicationService(serviceProvider), IFunctionAppService
{
    public async Task<Result<Dictionary<string, dynamic?>>> GetFunctionByFunctionKey(
     string key,
     string flow,
     string domain,
     Dictionary<string, string?>? headers = null,
     Dictionary<string, string?>? queryParameters = null,
     CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(domain);
        using (currentSchema.Change(flow))
        {
            Instance? instance = await instanceRepository.FindByKeyAsync(key, cancellationToken);
            return await BuildFunctionResponse(instance, key, flow, domain, headers, queryParameters, cancellationToken);
        }
    }
    
    public async Task<Result<Dictionary<string, dynamic?>>> GetFunctionByInstance(
       string key,
       string flow,
       string domain,
       string instanceKey,
       Dictionary<string, string?>? headers = null,
       Dictionary<string, string?>? queryParameters = null,
       CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(domain);
        using (currentSchema.Change(flow))
        {

            Instance? instance = Guid.TryParse(instanceKey, out var instanceId)
            ? await instanceRepository.FindAsync(instanceId, true, cancellationToken)
            : await instanceRepository.FindByKeyAsync(instanceKey, cancellationToken);
            return await BuildFunctionResponse(instance, key, flow, domain, headers, queryParameters, cancellationToken);
        }
    }

    public async Task<List<InstanceAndDataModel>> GetDomainFunctions(
        string domain,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(domain);
        var instance = await instanceRepository.FindByKeyAsync(key, cancellationToken);
        return await BuildFunctionResponseAsync(instance, key, flow, domain, cancellationToken);
    }

    private async Task<dynamic> BuildFunctionResponse(Instance? instance, string key,
       string flow,
       string domain,
       Dictionary<string, string?>? headers = null,
       Dictionary<string, string?>? queryParameters = null,
       CancellationToken cancellationToken = default)
    {
        Dictionary<string, object> response = new Dictionary<string, object>();
        using (currentSchema.Change(flow))
        {
            Function? function = await componentCacheStore.GetFunctionAsync(
                       runtimeInfoProvider.Domain,
                       key, string.Empty,
                       cancellationToken);
            var workflow = await componentCacheStore.GetFlowAsync(domain, flow, null, cancellationToken);

            // var scriptContext = new ScriptContext.Builder()
            //             .SetWorkflow(workflow)
            //             .SetInstance(instance!)
            //             .SetRuntime(runtimeInfoProvider)
            //             .Build();
            var scriptContext = await scriptContextFactory.NewBuilder()
.WithWorkflow(workflow)
.WithInstance(instance)
.WithRuntime(runtimeInfoProvider)
.WithBody(instance?.LatestData?.Data ?? new JsonData("{}"))
.WithHeaders(headers)
.WithQueryParameters(queryParameters)
.BuildAsync(cancellationToken);
            await taskExecutionService.ExecuteAsync(
                    function.GetExecuteTasks(),
                    null,
                    TaskTrigger.Extension,
                    scriptContext,
                    cancellationToken);
            var variableKeyFunction = function.Key.ToVariableName();
            var variableKeyTask = function.Task.Task.Key.ToVariableName();
            if (scriptContext.TaskResponse.TryGetValue(variableKeyTask, out var value))
            {
                // Try to extract Data from ScriptResponse if available
                try
                {
                    // Try to extract data property from JsonElement
                    response[variableKeyFunction] = value is JsonElement jsonElement && jsonElement.TryGetProperty("data", out var dataProperty) 
                        ? dataProperty 
                        : value!;
                }
                catch
                {
                    // If extraction fails, use the original value
                    response[variableKeyFunction] = value!;
                }

            }
            return response;
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<InstanceAndDataModel>>> GetDomainFunctionsAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(domain);
        var result = await instanceRepository.GetActiveDataListAsync(cancellationToken);
        return Result<List<InstanceAndDataModel>>.Ok(result);
    }

    /// <summary>
    /// Builds the function response using Railway pattern.
    /// Chain: Load Function → Load Workflow → Execute Tasks → Extract Response
    /// </summary>
    private Task<Result<Dictionary<string, dynamic?>>> BuildFunctionResponseAsync(
        Instance? instance,
        string functionKey,
        string flow,
        string domain,
        CancellationToken cancellationToken = default)
    {
        return componentCacheStore.GetFunctionAsync(runtimeInfoProvider.Domain, functionKey, string.Empty, cancellationToken)
            .BindAsync(function =>
                componentCacheStore.GetFlowAsync(domain, flow, null, cancellationToken)
                    .MapAsync(workflow => (function, workflow)))
            .ThenAsync(data => ExecuteFunctionAsync(data.function, data.workflow, instance, cancellationToken));
    }

    /// <summary>
    /// Executes the function tasks and extracts the response.
    /// </summary>
    private async Task<Result<Dictionary<string, dynamic?>>> ExecuteFunctionAsync(
        Function function,
        Definitions.Workflow workflow,
        Instance? instance,
        CancellationToken cancellationToken)
    {
        var scriptContext = await scriptContextFactory.NewBuilder()
            .WithWorkflow(workflow)
            .WithInstance(instance)
            .WithRuntime(runtimeInfoProvider)
            .WithBody(instance?.LatestData?.Data ?? new JsonData("{}"))
            .BuildAsync(cancellationToken);

        await taskExecutionService.ExecuteAsync(
            function.GetExecuteTasks(),
            null,
            TaskTrigger.Extension,
            scriptContext,
            cancellationToken);

        var response = ExtractFunctionResponse(function, scriptContext);
        return Result<Dictionary<string, dynamic?>>.Ok(response);
    }

    /// <summary>
    /// Extracts the function response from script context.
    /// </summary>
    private static Dictionary<string, dynamic?> ExtractFunctionResponse(
        Function function,
        ScriptContext scriptContext)
    {
        var response = new Dictionary<string, dynamic?>();
        var variableKeyFunction = function.Key.ToVariableName();
        var variableKeyTask = function.Task.Task.Key.ToVariableName();

        if (scriptContext.TaskResponse.TryGetValue(variableKeyTask, out var value))
        {
            response[variableKeyFunction] = value is JsonElement jsonElement &&
                                            jsonElement.TryGetProperty("data", out var dataProperty)
                ? dataProperty
                : value;
        }

        return response;
    }
}
