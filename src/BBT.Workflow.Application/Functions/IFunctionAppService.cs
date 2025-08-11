using BBT.Aether.Application;

namespace BBT.Workflow.Functions;

public interface IFunctionAppService : IApplicationService
{
    Task<Dictionary<string, dynamic?>> GetFunctionByFunctionKey(
        string key,
        string flow,
        string domain,
        CancellationToken cancellationToken = default);
        Task<List<Instances.InstanceAndDataModel>> GetDomainFunctions(
        string domain,
        CancellationToken cancellationToken = default);
        Task<Dictionary<string, dynamic?>> GetFunctionByInstance(
        string key,
        string flow,
        string domain,
        string instance,
        CancellationToken cancellationToken = default);
}