using System.Text.Json;

namespace BBT.Workflow.Definitions.CastHandlers;

/// <summary>
/// Processes workflow casting operations by delegating to appropriate handlers.
/// This class acts as a coordinator that selects the correct handler based on the workflow type
/// and orchestrates the workflow processing operation.
/// </summary>
/// <param name="handlers">A collection of workflow cast handlers available for processing.</param>
public sealed class WorkflowCastProcessor(IEnumerable<IWorkflowCastHandler> handlers)
{
    /// <summary>
    /// Asynchronously processes a workflow by finding the appropriate handler and executing the cast operation.
    /// </summary>
    /// <param name="workflow">The workflow type identifier that determines which handler to use.</param>
    /// <param name="reference">The reference object containing workflow metadata.</param>
    /// <param name="attributes">The JSON element containing the workflow attributes to be processed.</param>
    /// <param name="cancellationToken">The cancellation token to observe during the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no handler is found for the specified workflow type.</exception>
    /// <exception cref="ArgumentNullException">Thrown when workflow, reference, or attributes is null.</exception>
    public async Task ProcessAsync(string workflow, IReference reference, JsonElement attributes,
        CancellationToken cancellationToken)
    {
        var handler = handlers.FirstOrDefault(h => h.CanHandle(workflow));
        if (handler == null)
            throw new InvalidOperationException($"No handler found for workflow '{workflow}'.");

        await handler.HandleAsync(reference, attributes, cancellationToken);
    }
}