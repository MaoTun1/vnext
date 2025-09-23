namespace BBT.Workflow;

public static class WorkflowErrorCodes
{
    public const string NotFoundDomain = "Workflow:100001";
    public const string ConflictWorkflow = "Workflow:100002";
    public const string NotFoundInitialState = "Workflow:100003";
    public const string NotFoundTransition = "Workflow:100004";
    public const string InvalidState = "Workflow:100005";
    public const string RuntimeSchemaInvalidState = "Workflow:100006";
    public const string TransitionRuleFailed = "Workflow:100007";
    public const string SubFlowBlocked = "Workflow:100008";
    public const string TransitionLocked = "Workflow:100009";
    public const string UnauthorizedTransition = "Workflow:100010";
    public const string AutoTransitionFailed = "Workflow:100011";
}