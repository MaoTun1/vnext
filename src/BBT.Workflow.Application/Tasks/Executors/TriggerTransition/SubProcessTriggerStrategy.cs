using BBT.Aether.Guids;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Instances;
using BBT.Workflow.Runtime;
using BBT.Workflow.Scripting;
using BBT.Workflow.SubFlow;

namespace BBT.Workflow.Tasks.TriggerTransition;

/// <summary>
/// Strategy for handling SubProcess trigger type.
/// Triggers transitions on correlated SubFlow instances by starting a subprocess workflow.
/// </summary>
public sealed class SubProcessTriggerStrategy : ITriggerTransitionStrategy
{
    private readonly ISubflowStarter _subflowStarter;
    private readonly IGuidGenerator _guidGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubProcessTriggerStrategy"/> class.
    /// </summary>
    /// <param name="subflowStarter">Service for starting SubFlow workflows.</param>
    /// <param name="guidGenerator">Generator for creating unique identifiers.</param>
    public SubProcessTriggerStrategy(
        ISubflowStarter subflowStarter,
        IGuidGenerator guidGenerator
        )
    {
        _subflowStarter = subflowStarter;
        _guidGenerator = guidGenerator;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        TriggerTransitionTask task,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        // Create correlation for SubProcess
        var correlation = InstanceCorrelation.Create(
            _guidGenerator.Create(),
            context.Instance.Id,
            context.Instance.GetCurrentState,
            _guidGenerator.Create(),
            SubFlowType.SubProcess.Code,
            task.TriggerDomain,
            task.TriggerKey!,
            task.TriggerVersion);
        context.Instance.AddCorrelation(correlation);

        // Create a SubFlow reference for the subprocess
        var subFlowReference = new Reference(
            task.TriggerKey!,
            task.TriggerDomain,
            RuntimeSysSchemaInfo.Flows,
            task.TriggerVersion ?? string.Empty);

        // Start the SubProcess using simplified SubStartAsync method
        await _subflowStarter.SubStartAsync(
            context.Workflow,
            context.Instance,
            subFlowReference,
            context.Transition,
            correlation,
            SubFlowType.SubProcess.Code,
            cancellationToken);
    }
}

