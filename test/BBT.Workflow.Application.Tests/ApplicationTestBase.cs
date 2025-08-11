using BBT.Aether.Testing;

namespace BBT.Workflow;

public class ApplicationTestBase<TEntry> : WorkflowTestBase<TEntry>
    where TEntry : ModuleEntryPointBase, new();