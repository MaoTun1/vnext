using BBT.Workflow.Definitions;

namespace BBT.Workflow.Instances;

public class InstanceConstants
{
    public const int MaxKeyLength = WorkflowConstants.MaxKeyLength;
    public const int MaxStatusLength = 3;
    public const int TransitionLockExpiryInSeconds = 300; // 5 minutes
}

public class InstanceActionConstants
{
    public const int MaxStatusLength = 70;
}

public class InstanceJobConstants
{
    public const int MaxJobIdLength = 400;
    public const int MaxJobNameLength = 125;
    public const int MaxExpressionValueLength = 500;
}