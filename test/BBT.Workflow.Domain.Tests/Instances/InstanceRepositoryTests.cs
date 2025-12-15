using BBT.Aether.Testing;

namespace BBT.Workflow.Instances;

public abstract class InstanceRepositoryTests<TEntry> : DomainTestBase<TEntry>
    where TEntry : ModuleEntryPointBase, new();