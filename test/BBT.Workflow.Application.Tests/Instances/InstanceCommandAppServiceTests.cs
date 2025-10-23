using BBT.Aether.Testing;

namespace BBT.Workflow.Instances;

public abstract class InstanceCommandAppServiceTests<TEntry> : ApplicationTestBase<TEntry>
    where TEntry : ModuleEntryPointBase, new()
{
    private readonly IInstanceCommandAppService _instanceCommandAppService;
    private readonly TestData _testData;

    protected InstanceCommandAppServiceTests()
    {
        _instanceCommandAppService = GetRequiredService<IInstanceCommandAppService>();
        _testData = GetRequiredService<TestData>();
    }
    
}