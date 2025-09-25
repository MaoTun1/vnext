using BBT.Aether;

namespace BBT.Workflow.ExceptionHandling;

public class ConfigInvalidException(Guid instanceId): UserFriendlyException(code: WorkflowErrorCodes.ConfigInvalid, message: $"SubFlow configuration not found for parent instance {instanceId}");
