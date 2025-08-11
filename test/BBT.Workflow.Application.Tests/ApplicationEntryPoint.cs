using BBT.Aether.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BBT.Workflow;

public class ApplicationEntryPoint : ModuleEntryPointBase
{
    public override void Load(IServiceCollection services)
    {
        services.AddApplicationModule();
    }
}
