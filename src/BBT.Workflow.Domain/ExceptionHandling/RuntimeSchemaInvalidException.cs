using BBT.Aether;

namespace BBT.Workflow.ExceptionHandling;

public class RuntimeSchemaInvalidException() : UserFriendlyException(
    code: WorkflowErrorCodes.RuntimeSchemaInvalidState,
    message: "Only defined system flows can be published");