using BBT.Workflow.Shared;

namespace BBT.Workflow.Instances;

public sealed class InstanceServiceResponse<T>(T data) : ServiceResponse<T, InstanceServiceResponse<T>>(data);