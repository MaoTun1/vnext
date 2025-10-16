using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.Instances;
using BBT.Workflow.Scripting;
using BBT.Workflow.Tasks;
using BBT.Workflow.Telemetry;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BBT.Workflow.Execution.Pipeline.Steps;

/// <summary>
/// Pipeline step that executes the transition's OnExecute tasks.
/// These tasks run before the state change occurs.
/// Uses Result pattern for exception-free error handling.
/// </summary>
public sealed class RunOnExecuteTasksStep(
    ITaskOrchestrationService taskOrchestrationService,
    IScriptContextFactory scriptContextFactory,
    IInstanceRepository instanceRepository,
    ILogger<RunOnExecuteTasksStep> logger) : ITransitionStep
{
    /// <inheritdoc />
    public int Order => LifecycleOrder.OnExecute;

    /// <inheritdoc />
    public async Task<Result<StepOutcome>> ExecuteAsync(TransitionExecutionContext context, CancellationToken cancellationToken)
    {
        if (!context.Transition.OnExecutionTasks.Any())
        {
            logger.LogTrace("No OnExecute tasks for transition {TransitionKey}", context.TransitionKey);
            return Result<StepOutcome>.Ok(StepOutcome.Continue());
        }

        var sw = Stopwatch.StartNew();
        logger.LogDebug("Executing {TaskCount} OnExecute tasks for transition {TransitionKey} on instance {InstanceId}",
            context.Transition.OnExecutionTasks.Count, context.TransitionKey, context.InstanceId);

        return await ResultExtensions.TryAsync<StepOutcome>(async ct =>
        {
            // Get or build script context
            var scriptContext = context.GetOrBuildScriptContext(() => 
                CreateScriptContext(context));

            // Get the transition record from previous step
            var instanceTransitionId = context.Items.TryGetValue("TransitionRecordId", out var record) 
                ? record as Guid? 
                : null;

            // Execute the tasks
            await taskOrchestrationService.ExecuteAsync(
                context.Transition.OnExecutionTasks,
                instanceTransitionId,
                TaskTrigger.OnExecute,
                scriptContext,
                ct);

            context.ApplyScriptContextChanges(scriptContext);
            
            await instanceRepository.UpdateAsync(context.Instance, true, ct);

            sw.Stop();
            logger.LogDebug("Completed {TaskCount} OnExecute tasks for transition {TransitionKey} in {ElapsedMs}ms",
                context.Transition.OnExecutionTasks.Count, context.TransitionKey, sw.ElapsedMilliseconds);
            
            return StepOutcome.Continue();
        },
        cancellationToken,
        ex =>
        {
            sw.Stop();
            logger.LogError(ex, "OnExecute tasks failed for transition {TransitionKey} after {ElapsedMs}ms",
                context.TransitionKey, sw.ElapsedMilliseconds);
            return Error.Failure(
                WorkflowErrorCodes.ExecutionStepFailed,
                $"OnExecute tasks execution failed: {ex.Message}",
                ex.GetType().Name);
        });
    }

    /// <summary>
    /// Creates a script context for task execution.
    /// </summary>
    private ScriptContext CreateScriptContext(TransitionExecutionContext context)
    {
        return scriptContextFactory.NewBuilder()
            .WithWorkflow(context.Workflow)
            .WithInstance(context.Instance)
            .WithTransition(context.Transition)
            .WithBody(context.Data)
            .WithHeaders(context.Headers.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value))
            .BuildAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}
