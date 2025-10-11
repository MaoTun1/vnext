using BBT.Workflow.Execution.ReEntry;

namespace BBT.Workflow.Execution;

public sealed class PipelineDirectives
{
    // Where to start? (Subflow completion → Schedule)
    public int? ResumeFromOrder { get; private set; }

    // Epilogue policy (Subflow started → Skip)
    public EpilogueMode Epilogue { get; private set; } = EpilogueMode.Run;

    // Command queue for inline auto chain
    public Queue<ReentryCommand> InlineAutoQueue { get; } = new();

    // Did it reach the terminal?
    public bool TerminalReached { get; private set; }
    
    // Is this a SubFlow resume scenario?
    public bool IsSubFlowResume { get; private set; }

    public void RequestResumeFrom(int order) => ResumeFromOrder = order;

    public int? ConsumeResumeFrom()
    {
        var t = ResumeFromOrder;
        ResumeFromOrder = null;
        return t;
    }

    public void RequestEpilogue(EpilogueMode mode) => Epilogue = mode;
    public void MarkTerminal() => TerminalReached = true;
    public void EnqueueInlineAuto(ReentryCommand command) => InlineAutoQueue.Enqueue(command);
    public void MarkAsSubFlowResume() => IsSubFlowResume = true;
}