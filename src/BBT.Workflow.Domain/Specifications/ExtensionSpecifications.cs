using BBT.Workflow.Definitions;

public class ExtensionIsRequestedSpecification : BaseSpecification<Extension>
{
    public ExtensionIsRequestedSpecification(string[]? extensionRequested)
    {
        Criteria = w => (w.Type == ExtensionType.Global || w.Type == ExtensionType.DefinedFlows ||
        (w.Type == ExtensionType.DefinedFlowAndRequested && extensionRequested != null && extensionRequested.Contains(w.Key))
        || (w.Type == ExtensionType.GlobalAndRequested && extensionRequested != null && extensionRequested.Contains(w.Key)));
    }
}
public class ExtensionScopeEnumValidation : BaseSpecification<Extension>
{
    public ExtensionScopeEnumValidation(ExtensionScope extensionScope)
    {
        Criteria = i => i.Scope == ExtensionScope.Everywhere || i.Scope == extensionScope;
    }
}
public class ExtensionIsTaskNotNullSpecification : BaseSpecification<Extension>
{
    public ExtensionIsTaskNotNullSpecification()
    {
        Criteria = i => i.Task != null;

    }
}
public class ExtensionIsTasKTypeGlobalOrFlow : BaseSpecification<Extension>
{
    public ExtensionIsTasKTypeGlobalOrFlow()
    {
        Criteria = w => w.Type == ExtensionType.Global || w.Type == ExtensionType.DefinedFlows;
 
    }
}