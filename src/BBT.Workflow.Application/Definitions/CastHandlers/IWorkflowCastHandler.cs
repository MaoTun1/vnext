using System.Text.Json;

namespace BBT.Workflow.Definitions.CastHandlers;

/// <summary>
/// Defines a contract for handling workflow casting operations.
/// Implementations of this interface are responsible for processing specific workflow types 
/// and updating the cache context with deserialized workflow data.
/// </summary>
public interface IWorkflowCastHandler
{
    /// <summary>
    /// Determines whether this handler can process the specified workflow type.
    /// </summary>
    /// <param name="workflow">The workflow type identifier to check.</param>
    /// <returns>True if this handler can process the workflow type; otherwise, false.</returns>
    bool CanHandle(string workflow);
    
    /// <summary>
    /// Asynchronously processes the workflow data by deserializing attributes and updating the cache.
    /// </summary>
    /// <param name="reference">The reference object containing workflow metadata.</param>
    /// <param name="attributes">The JSON element containing the workflow attributes to be processed.</param>
    /// <param name="cancellationToken">The cancellation token to observe during the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous handling operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when reference or attributes is null.</exception>
    /// <exception cref="JsonException">Thrown when JSON deserialization fails.</exception>
    Task HandleAsync(IReference reference, JsonElement attributes, CancellationToken cancellationToken);
}