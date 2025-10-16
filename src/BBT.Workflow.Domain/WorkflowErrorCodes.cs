namespace BBT.Workflow;

/// <summary>
/// Defines workflow-specific error codes and prefixes for consistent error categorization.
/// Error codes follow the pattern: prefix.category:code
/// </summary>
public static class WorkflowErrorCodes
{
    #region Error Prefixes
    
    /// <summary>
    /// Error code prefixes for categorizing errors by type.
    /// Used for automatic HTTP status code mapping.
    /// </summary>
    public static class Prefixes
    {
        public const string Validation = "validation";
        public const string NotFound = "notfound";
        public const string Conflict = "conflict";
        public const string Unauthorized = "auth.unauthorized";
        public const string Forbidden = "auth.forbidden";
        public const string Auth = "auth";
        public const string Transient = "transient";
        public const string Dependency = "dep";
        public const string Failure = "failure";
    }
    
    #endregion
    
    #region Instance Errors (100xxx)
    
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
    public const string ConfigInvalid = "Workflow:100012";
    public const string NotFoundInstanceData = "Workflow:100013";
    public const string AutoTransitionConditionNotMet = "Workflow:100014";
    
    #endregion
    
    #region Execution Errors (200xxx)
    
    public const string ExecutionPipelineFailed = "Workflow:200001";
    public const string ExecutionStepFailed = "Workflow:200002";
    public const string ExecutionHandlerFailed = "Workflow:200003";
    public const string ExecutionStrategyFailed = "Workflow:200004";
    public const string ExecutionContextInvalid = "Workflow:200005";
    
    #endregion
}