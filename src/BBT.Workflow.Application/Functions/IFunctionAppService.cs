using BBT.Aether.Application;

namespace BBT.Workflow.Functions;

public interface IFunctionAppService : IApplicationService
{
    Task<Dictionary<string, dynamic?>> GetFunctionByFunctionKey(
        string key,
        string flow,
        string domain,
        Dictionary<string, string?>? headers = null,
        Dictionary<string, string?>? queryParameters = null,
        CancellationToken cancellationToken = default);
        Task<List<Instances.InstanceAndDataModel>> GetDomainFunctions(
        string domain,
        CancellationToken cancellationToken = default);
        Task<Dictionary<string, dynamic?>> GetFunctionByInstance(
        string key,
        string flow,
        string domain,
        string instance,
        Dictionary<string, string?>? headers = null,
        Dictionary<string, string?>? queryParameters = null,
        CancellationToken cancellationToken = default);
}