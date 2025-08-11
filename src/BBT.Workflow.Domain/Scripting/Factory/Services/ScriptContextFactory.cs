using BBT.Workflow.Caching;
using BBT.Workflow.Instances;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Default implementation of IScriptContextFactory that provides fluent builder capabilities
/// and handles ScriptContext creation with various data sources.
/// </summary>
public sealed class ScriptContextFactory(
    IComponentCacheStore componentCacheStore,
    IInstanceRepository instanceRepository) : IScriptContextFactory
{
    /// <summary>
    /// Creates a new fluent builder for constructing ScriptContext instances.
    /// </summary>
    /// <returns>A new ScriptContextBuilder instance for fluent configuration.</returns>
    public IScriptContextBuilder NewBuilder()
    {
        return new ScriptContextBuilder(componentCacheStore, instanceRepository);
    }
}
