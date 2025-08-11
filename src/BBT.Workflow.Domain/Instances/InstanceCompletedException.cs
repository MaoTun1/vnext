using BBT.Aether.Domain.Entities;

namespace BBT.Workflow.Instances;

public class InstanceCompletedException(Guid id) : EntityNotFoundException(typeof(Instance), id);