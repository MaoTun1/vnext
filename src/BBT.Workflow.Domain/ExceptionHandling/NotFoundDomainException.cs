using BBT.Aether;

namespace BBT.Workflow.ExceptionHandling;

public class NotFoundDomainException(string requestedDomain, string expectedDomain) : UserFriendlyException(
    code: WorkflowErrorCodes.NotFoundDomain,
    message: $"Invalid domain: \"{requestedDomain}\". Expected domain is \"{expectedDomain}\".");