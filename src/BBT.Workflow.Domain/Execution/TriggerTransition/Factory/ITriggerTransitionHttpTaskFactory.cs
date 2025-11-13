using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Execution.TriggerTransition;

/// <summary>
/// Factory for creating HTTP tasks used in trigger transition strategies.
/// </summary>
public interface ITriggerTransitionHttpTaskFactory
{
    /// <summary>
    /// Creates an HttpTask for calling workflow instance endpoints.
    /// </summary>
    /// <param name="triggerTask">The trigger transition task containing configuration.</param>
    /// <param name="context">The script context containing headers and body data.</param>
    /// <param name="path">The API endpoint path to call (without base URL and version).</param>
    /// <param name="method">The HTTP method (POST, PATCH, etc.).</param>
    /// <returns>A configured HttpTask ready to execute.</returns>
    HttpTask CreateHttpTask(
        TriggerTransitionTask triggerTask,
        ScriptContext context,
        string path,
        string method);
}

