using System.Text;
using System.Text.Json;
using BBT.Aether.Application;
using BBT.Aether.Application.Services;
using BBT.Aether.Domain.Entities;
using BBT.Workflow.Caching;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using BBT.Workflow.Schemas;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;

namespace BBT.Workflow.Functions;

public sealed class FunctionAppService(IServiceProvider serviceProvider,
    ICurrentSchema currentSchema,
    IRuntimeInfoProvider runtimeInfoProvider,
    IInstanceRepository instanceRepository,
      IScriptContextFactory scriptContextFactory,
    IComponentCacheStore componentCacheStore, ITaskOrchestrationService taskExecutionService) : ApplicationService(serviceProvider), IFunctionAppService
{
    public async Task<Dictionary<string, dynamic?>> GetFunctionByFunctionKey(
     string key,
     string flow,
     string domain,
     CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(domain);
        using (currentSchema.Change(flow))
        {
            Instance? instance = await instanceRepository.FindByKeyAsync(key, cancellationToken);
            return await BuildFunctionResponse(instance, key, flow, domain, cancellationToken);
        }

    }
    public async Task<Dictionary<string, dynamic?>> GetFunctionByInstance(
       string key,
       string flow,
       string domain,
       string instanceKey,
       CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(domain);
        using (currentSchema.Change(flow))
        {

            Instance? instance = Guid.TryParse(instanceKey, out var instanceId)
            ? await instanceRepository.FindAsync(instanceId, true, cancellationToken)
            : await instanceRepository.FindByKeyAsync(instanceKey, cancellationToken);
            return await BuildFunctionResponse(instance, key, flow, domain, cancellationToken);
        }
    }
    public async Task<List<InstanceAndDataModel>> GetDomainFunctions(
        string domain,
        CancellationToken cancellationToken = default)
    {
        runtimeInfoProvider.Check(domain);
        using (currentSchema.Change(RuntimeSysSchemaInfo.Functions))
        {
            return await instanceRepository.GetActiveDataListAsync(cancellationToken);
        }

    }
    private async Task<dynamic> BuildFunctionResponse(Instance? instance, string key,
       string flow,
       string domain, CancellationToken cancellationToken = default)
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


}