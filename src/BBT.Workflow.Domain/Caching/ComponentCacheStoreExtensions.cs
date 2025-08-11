using BBT.Workflow.Definitions;

namespace BBT.Workflow.Caching;

public static class ComponentCacheStoreExtensions
{
    public static Task<Definitions.Workflow> GetFlowAsync(this IComponentCacheStore store, IReference reference,
        CancellationToken cancellationToken = default)
        => store.GetFlowAsync(reference.Domain, reference.Key, reference.Version, cancellationToken);

    public static Task<WorkflowTask> GetTaskAsync(this IComponentCacheStore store, IReference reference,
        CancellationToken cancellationToken = default)
        => store.GetTaskAsync(reference.Domain, reference.Key, reference.Version, cancellationToken);

    public static Task<SchemaDefinition> GetSchemaAsync(this IComponentCacheStore store, IReference reference,
        CancellationToken cancellationToken = default)
        => store.GetSchemaAsync(reference.Domain, reference.Key, reference.Version, cancellationToken);

    public static Task<Function> GetFunctionAsync(this IComponentCacheStore store, IReference reference,
        CancellationToken cancellationToken = default)
        => store.GetFunctionAsync(reference.Domain, reference.Key, reference.Version, cancellationToken);

    public static Task<View> GetViewAsync(this IComponentCacheStore store, IReference reference,
        CancellationToken cancellationToken = default)
        => store.GetViewAsync(reference.Domain, reference.Key, reference.Version, cancellationToken);
    public static Task<Extension> GetExtensionAsync(this IComponentCacheStore store, IReference reference,
        CancellationToken cancellationToken = default)
        => store.GetExtensionAsync(reference.Domain, reference.Key, reference.Version, cancellationToken);
}