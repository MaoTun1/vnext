using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;

namespace BBT.Workflow.Tasks.Extensions;

/// <summary>
/// Extension methods for ScriptContextFactory to provide additional convenience methods.
/// </summary>
public static class ScriptContextFactoryExtensions
{
    /// <summary>
    /// Extension method that creates a ScriptContext directly from TaskExecutionRequestInput
    /// using the factory pattern for cleaner service integration.
    /// </summary>
    /// <param name="factory">The script context factory instance.</param>
    /// <param name="input">The task execution request input.</param>
    /// <param name="runtimeInfoProvider">The runtime information provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A configured ScriptContext instance.</returns>
    public static async Task<ScriptContext> CreateFromTaskRequestAsync(
        this IScriptContextFactory factory,
        TaskExecutionRequestInput input,
        IRuntimeInfoProvider runtimeInfoProvider,
        CancellationToken cancellationToken = default)
    {
        return await factory.NewBuilder()
            .WithRuntime(runtimeInfoProvider)
            .WithWorkflow(input.Context.Workflow)
            .WithInstance(input.Context.InstanceId, true)
            .WithTransition(input.Context.TransitionKey)
            .WithBody(input.Context.Body)
            .WithHeaders(input.Context.Headers)
            .WithRouteValues(input.Context.RouteValues)
            .WithTaskResponse(input.Context.TaskResponse)
            .WithMetadata(input.Context.MetaData ?? new Dictionary<string, object>())
            .WithDefinitions(input.Context.Definitions ?? new Dictionary<string, object>())
            .BuildAsync(cancellationToken);
    }
}