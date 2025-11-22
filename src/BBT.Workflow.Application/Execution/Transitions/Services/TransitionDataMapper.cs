using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using BBT.Aether.Results;

namespace BBT.Workflow.Execution.Transitions.Services;

/// <summary>
/// Service responsible for mapping transition payload data to instance data.
/// Handles optional transition mapping scripts to transform payload before adding to instance.
/// </summary>
public sealed class TransitionDataMapper(
    IScriptEngine scriptEngine,
    IScriptContextFactory scriptContextFactory) : ITransitionDataMapper
{
    /// <inheritdoc />
    public async Task<Result<object?>> MapTransitionDataAsync(
        object? payload,
        Transition? transition,
        Definitions.Workflow workflow,
        Instance instance,
        IRuntimeInfoProvider runtimeInfoProvider,
        IReadOnlyDictionary<string, string?>? headers = null,
        CancellationToken cancellationToken = default)
    {
        // If no mapping is defined, return payload as-is converted to JsonData
        if (transition?.Mapping == null)
        {
            return Result<object?>.Ok(payload);
        }

        try
        {
            // Compile the mapping script to ITransitionMapping interface
            var mappingInstance = await scriptEngine.CompileToInstanceAsync<ITransitionMapping>(
                transition.Mapping.DecodedCode,
                cancellationToken: cancellationToken);

            // Create ScriptContext for the mapping handler
            var scriptContext = await scriptContextFactory.NewBuilder()
                .WithRuntime(runtimeInfoProvider)
                .WithWorkflow(workflow)
                .WithInstance(instance)
                .WithTransition(transition)
                .WithBody(payload)
                .WithHeaders(headers?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
                .BuildAsync(cancellationToken);

            // Execute the mapping handler
            var mappedData = await mappingInstance.Handler(scriptContext);
            return Result<object?>.Ok(mappedData);
        }
        catch (Exception ex)
        {
            return Result<object?>.Fail(Error.Failure(
                WorkflowErrorCodes.ExecutionStepFailed,
                $"Failed to execute transition mapping: {ex.Message}",
                ex.GetType().Name));
        }
    }
}
