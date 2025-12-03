using BBT.Aether.Application;
using BBT.Aether.Results;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Functions;

/// <summary>
/// Application service for function operations.
/// All methods return Result types following Railway pattern.
/// </summary>
public interface IFunctionAppService : IApplicationService
{
    /// <summary>
    /// Gets function data by function key.
    /// </summary>
    Task<Result<Dictionary<string, dynamic?>>> GetFunctionByFunctionKeyAsync(
        string key,
        string flow,
        string domain,
        Dictionary<string, string?>? headers = null,
        Dictionary<string, string?>? queryParameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets function data by instance key or ID.
    /// </summary>
    Task<Result<Dictionary<string, dynamic?>>> GetFunctionByInstanceAsync(
        string key,
        string flow,
        string domain,
        string instance,
        Dictionary<string, string?>? headers = null,
        Dictionary<string, string?>? queryParameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active domain functions.
    /// </summary>
    Task<Result<List<InstanceAndDataModel>>> GetDomainFunctionsAsync(
        string domain,
        CancellationToken cancellationToken = default);
}