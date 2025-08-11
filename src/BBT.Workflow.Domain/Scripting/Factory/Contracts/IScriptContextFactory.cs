namespace BBT.Workflow.Scripting;

/// <summary>
/// Generic factory responsible for creating ScriptContext instances from various input sources.
/// This factory provides a fluent builder interface that can be used across different application services.
/// </summary>
public interface IScriptContextFactory
{
    /// <summary>
    /// Creates a new fluent builder for constructing ScriptContext instances.
    /// This builder can be configured with various data sources and then built into a ScriptContext.
    /// </summary>
    /// <returns>A new ScriptContextBuilder instance for fluent configuration.</returns>
    IScriptContextBuilder NewBuilder();
}