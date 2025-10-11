namespace BBT.Workflow.Execution.Pipeline;

public sealed class StepOutcome
{
    public bool StopPipeline { get; init; } // stop completely
    public int? SkipToOrder { get; init; } // continue in a certain order
    public string? NextTransitionKey { get; init; } // redirect to another transition in the same call
    public Action<PipelineDirectives>? MutateDirectives { get; init; } // update directive

    public static StepOutcome Continue() => new();
    public static StepOutcome Stop() => new() { StopPipeline = true };
    public static StepOutcome SkipTo(int order) => new() { SkipToOrder = order };
    public static StepOutcome With(Action<PipelineDirectives> fx) => new() { MutateDirectives = fx };
}