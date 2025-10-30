namespace BBT.Workflow;

public interface IViewDefinition
{
    string[] Extensions { get; }
    bool LoadData { get; }
    Reference View { get; }

}

public interface IViewDefinitionSetter
{
    void SetViewDefinition(IViewDefinition viewDefinition);
}

public sealed class ViewDefinition(
    string[] extensions,
    bool loadData,
    Reference view)
    : IViewDefinition
{
    /// <summary>
    /// Extensions to be loaded with the view.
    /// </summary>
    public string[] Extensions { get; private set; } = extensions;
    /// <summary>
    /// Load Data if true, otherwise only the view is loaded.
    /// </summary>
    public bool LoadData { get; private set; } = loadData;

    /// <summary>
    /// View to be loaded.
    /// </summary>
    public Reference View { get; private set; } = view;
}

public static class ViewDefinitionExtensions
{
    public static ViewDefinition ToReference(this IViewDefinition viewDefinition)
    {
        return new ViewDefinition(
            viewDefinition.Extensions,
            viewDefinition.LoadData,
            viewDefinition.View);
    }
}