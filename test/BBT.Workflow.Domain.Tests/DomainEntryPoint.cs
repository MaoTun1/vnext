using BBT.Aether.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BBT.Workflow;

public class DomainEntryPoint : ModuleEntryPointBase
{
    public override void Load(IServiceCollection services)
    {
        services.AddDomainModule();
    }
}