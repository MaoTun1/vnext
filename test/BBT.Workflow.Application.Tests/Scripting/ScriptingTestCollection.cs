using Xunit;

namespace BBT.Workflow.Scripting;

/// <summary>
/// Defines a test collection to ensure scripting tests run sequentially.
/// This prevents static state interference in ScriptHelper between test classes.
/// </summary>
[CollectionDefinition("ScriptingTests", DisableParallelization = true)]
public class ScriptingTestCollection
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

