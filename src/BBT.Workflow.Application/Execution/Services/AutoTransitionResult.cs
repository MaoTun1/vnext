using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Represents the result of automatic transition execution operations.
/// Contains information about execution success and the final instance state.
/// </summary>
public sealed class AutoTransitionResult
{
    /// <summary>
    /// Gets a value indicating whether any automatic transition was executed successfully.
    /// </summary>
    public bool HasExecutedTransition { get; init; }

    /// <summary>
    /// Gets the refreshed instance after automatic transitions, if any were executed.
    /// This instance reflects the latest state after all automatic transitions completed.
    /// </summary>
    public Instance? RefreshedInstance { get; init; }

    /// <summary>
    /// Gets a value indicating whether the instance was completed during automatic transitions.
    /// </summary>
    public bool IsCompleted => RefreshedInstance?.IsCompleted ?? false;

    /// <summary>
    /// Creates a result indicating no automatic transitions were available or executed.
    /// </summary>
    /// <param name="originalInstance">The original instance that was not modified</param>
    /// <returns>An AutoTransitionResult indicating no transitions were executed</returns>
    public static AutoTransitionResult NoTransitionsExecuted(Instance originalInstance)
    {
        return new AutoTransitionResult
        {
            HasExecutedTransition = false,
            RefreshedInstance = originalInstance
        };
    }

    /// <summary>
    /// Creates a result indicating automatic transitions were executed successfully.
    /// </summary>
    /// <param name="refreshedInstance">The instance after automatic transitions were executed</param>
    /// <returns>An AutoTransitionResult indicating successful transition execution</returns>
    public static AutoTransitionResult TransitionsExecuted(Instance refreshedInstance)
    {
        return new AutoTransitionResult
        {
            HasExecutedTransition = true,
            RefreshedInstance = refreshedInstance
        };
    }
}

