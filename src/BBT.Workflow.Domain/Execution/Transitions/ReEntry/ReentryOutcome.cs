namespace BBT.Workflow.Execution.ReEntry;

public sealed record class ReentryOutcome(
    bool InlineExecuted,
    bool Succeeded,
    string? NewState,
    string? NextTransitionKey,
    int? ResumeFromOrder);