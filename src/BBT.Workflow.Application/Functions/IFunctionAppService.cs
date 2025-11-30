using BBT.Aether.Application;
using BBT.Aether.Results;

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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets function data by instance key or ID.
    /// </summary>
    Task<Result<Dictionary<string, dynamic?>>> GetFunctionByInstanceAsync(
        string key,
        string flow,
        string domain,
        string instance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active domain functions.
    /// </summary>
    Task<Result<List<Instances.InstanceAndDataModel>>> GetDomainFunctionsAsync(
        string domain,
        CancellationToken cancellationToken = default);
}