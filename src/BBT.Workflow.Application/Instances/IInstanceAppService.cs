using BBT.Aether.Application;
using Microsoft.AspNetCore.Http;

namespace BBT.Workflow.Instances;

/// <summary>
/// Combined interface for backward compatibility. 
/// Consider using IInstanceCommandAppService and IInstanceQueryAppService separately for better separation of concerns.
/// </summary>
public interface IInstanceAppService : IInstanceCommandAppService, IInstanceQueryAppService
{
}