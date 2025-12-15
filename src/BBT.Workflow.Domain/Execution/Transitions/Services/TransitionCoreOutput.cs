using BBT.Workflow.Instances;

namespace BBT.Workflow.Execution.Services;

/// <summary>
/// Output from core transition execution containing both the transition output
/// and the directives snapshot for post-commit inline auto chain processing.
/// </summary>
/// <param name="Output">The transition output containing instance ID and status.</param>
/// <param name="DirectivesSnapshot">Snapshot of pipeline directives including inline auto queue.</param>
public sealed record TransitionCoreOutput(TransitionOutput Output, DirectivesSnapshot DirectivesSnapshot);

