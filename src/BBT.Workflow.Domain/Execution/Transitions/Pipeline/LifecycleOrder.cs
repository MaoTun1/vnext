namespace BBT.Workflow.Execution.Pipeline;

/// <summary>
/// Defines the standard execution order constants for transition lifecycle steps.
/// These constants ensure a deterministic and documented execution sequence.
/// </summary>
public static class LifecycleOrder
{
    /// <summary>
    /// Order for creating the transition record in the database.
    /// This should be the first step to track the transition attempt.
    /// </summary>
    public const int CreateTransition = 10;
    
    /// <summary>
    /// Order for executing transition's OnExecute tasks.
    /// These tasks run before the state change occurs.
    /// </summary>
    public const int OnExecute = 20;
    
    /// <summary>
    /// Order for executing current state's OnExit tasks.
    /// These tasks run when leaving the current state.
    /// </summary>
    public const int OnExit = 30;
    
    /// <summary>
    /// Order for changing the instance state.
    /// This is the core state transition operation.
    /// </summary>
    public const int ChangeState = 40;
    
    /// <summary>
    /// Order for executing target state's OnEntry tasks.
    /// These tasks run when entering the new state.
    /// </summary>
    public const int OnEntry = 50;
    
    /// <summary>
    /// Order for handling SubFlow operations or finishing the workflow.
    /// Manages workflow completion or sub-process initiation.
    /// </summary>
    public const int FinishOrSubflow = 60;
    
    /// <summary>
    /// Order for scheduling future transitions.
    /// Enqueues scheduled transitions based on timers.
    /// </summary>
    public const int Schedule = 70;
    
    /// <summary>
    /// Order for executing automatic transitions.
    /// Evaluates and triggers automatic transitions based on conditions.
    /// </summary>
    public const int Auto = 80;
    
    /// <summary>
    /// Order for finalizing the transition.
    /// Updates transition record and performs cleanup operations.
    /// </summary>
    public const int Finalize = 90;
}
