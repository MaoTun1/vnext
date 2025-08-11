using BBT.Aether;

namespace BBT.Workflow.ExceptionHandling;

public class NotFoundStateException(string flow, string state) : UserFriendlyException(code: WorkflowErrorCodes.NotFoundInitialState,
    message: $"No {state} state found for workflow \"{flow}\"");

public class InvalidStateException(string transition, string? fromState = "N/A", string? currentState = "N/A") : UserFriendlyException(
    code: WorkflowErrorCodes.InvalidState,
    message:
    $"Transition \"{transition}\" is not valid for the current state. Expected state: {fromState}, Current state: {currentState}");