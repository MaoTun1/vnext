using BBT.Workflow.Scripting;

namespace BBT.Workflow.Definitions;

/// <summary>
/// Interface for tasks that support global mapping configuration.
/// Global tasks can have an IMapping instance that is used instead of script code.
/// </summary>
public interface IGlobalTask
{
    /// <summary>
    /// Gets the mapping instance for this task. Can be null if no global mapping is configured.
    /// </summary>
    IMapping? Mapping { get; }
}

