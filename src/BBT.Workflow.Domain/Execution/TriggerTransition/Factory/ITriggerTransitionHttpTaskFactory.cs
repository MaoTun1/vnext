using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Execution.TriggerTransition;

/// <summary>
/// Factory for creating HTTP tasks used in trigger transition strategies.
/// Also provides utilities for resolving instance IDs.
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
    /// <returns>A Result containing a configured HttpTask or an error.</returns>
    Result<HttpTask> CreateHttpTask(
        TriggerTransitionTask triggerTask,
        ScriptContext context,
        string path,
        string method);

    /// <summary>
    /// Resolves the instance ID for a trigger task based on its configuration.
    /// </summary>
    /// <param name="task">The trigger transition task.</param>
    /// <param name="context">The script context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the resolved instance ID as a string or an error.</returns>
    /// <remarks>
    /// Resolution priority:
    /// 1. If TriggerInstanceId is provided, uses it directly
    /// 2. If TriggerKey is provided, queries the instance by key to get the ID
    /// 3. Otherwise, uses the current instance ID from context
    /// </remarks>
    Task<Result<string>> ResolveInstanceIdAsync(
        TriggerTransitionTask task,
        ScriptContext context,
        CancellationToken cancellationToken);
}

