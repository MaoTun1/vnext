using BBT.Aether;

namespace BBT.Workflow.ExceptionHandling;

public class ConflictException() : UserFriendlyException(code: WorkflowErrorCodes.ConflictWorkflow, message: "A record with the same version already exists.");