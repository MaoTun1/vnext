using BBT.Aether.Testing;

namespace BBT.Workflow;

public abstract class DomainTestBase<TEntry> : WorkflowTestBase<TEntry>
    where TEntry : ModuleEntryPointBase, new();