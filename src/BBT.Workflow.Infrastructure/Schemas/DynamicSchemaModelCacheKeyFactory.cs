using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BBT.Workflow.Schemas;

/// <summary>
/// Provides a custom model cache key factory that enables dynamic schema support in Entity Framework Core.
/// This class resolves the issue where EF Core's model cache doesn't account for different database schemas,
/// allowing the same DbContext type to work with multiple schemas by generating unique cache keys for each schema.
/// </summary>
/// <remarks>
/// <para><strong>Problem Background:</strong></para>
/// <para>
/// Entity Framework Core creates and caches the model (IModel) once per DbContext type for performance optimization.
/// This model cache is fixed by default and references the first created schema, causing issues in multi-schema scenarios:
/// </para>
/// <list type="bullet">
/// <item><description>First request with Schema A: Model is cached for Schema A</description></item>
/// <item><description>Second request with Schema B: Same cached model is used, but it still references Schema A</description></item>
/// <item><description>Operations like GenerateCreateScript() produce incorrect results for Schema B</description></item>
/// </list>
/// 
/// <para><strong>Solution:</strong></para>
/// <para>
/// This factory creates unique cache keys that include the schema name, ensuring that each schema gets its own
/// cached model instance. This allows the same DbContext type to work correctly with multiple database schemas.
/// </para>
/// 
/// <para>
/// Implementation based on: https://stackoverflow.com/questions/41979215/how-to-implement-imodelcachekeyfactory-in-ef-core
/// </para>
/// </remarks>
public class DynamicSchemaModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <summary>
    /// Creates a unique cache key for the Entity Framework Core model based on the database context and design-time flag.
    /// This method generates cache keys that include schema information, ensuring proper model caching for multi-schema scenarios.
    /// </summary>
    /// <param name="context">
    /// The <see cref="DbContext"/> instance for which the cache key is being created.
    /// The schema information is extracted from this context if it implements <see cref="WorkflowDbContext"/>.
    /// </param>
    /// <param name="designTime">
    /// A boolean value indicating whether the model is being created at design time (e.g., during migrations)
    /// or at runtime. This affects how the model is constructed and cached.
    /// </param>
    /// <returns>
    /// A <see cref="CoreModelCacheKey"/> object that uniquely identifies the model cache entry for the given
    /// context and design-time configuration. The key includes schema information to ensure proper cache separation.
    /// </returns>
    /// <remarks>
    /// The method creates a specialized cache key that considers:
    /// <list type="bullet">
    /// <item><description>The base DbContext type and configuration</description></item>
    /// <item><description>The current schema name from the WorkflowDbContext</description></item>
    /// <item><description>The design-time flag for migration scenarios</description></item>
    /// </list>
    /// 
    /// This ensures that different schemas get separate model cache entries, preventing cross-schema contamination
    /// in the Entity Framework Core model cache.
    /// </remarks>
    public object Create(DbContext context, bool designTime)
        => new CoreModelCacheKey(context, designTime);
}

/// <summary>
/// Represents a specialized model cache key that includes schema information for proper multi-schema support.
/// This class extends the base <see cref="ModelCacheKey"/> to include schema name in equality comparisons and hash code generation,
/// ensuring that different database schemas get separate cache entries in Entity Framework Core's model cache.
/// </summary>
/// <remarks>
/// The CoreModelCacheKey addresses the fundamental issue in Entity Framework Core where the default model caching
/// mechanism doesn't account for different database schemas. By including the schema name in the cache key,
/// this class ensures that:
/// <list type="bullet">
/// <item><description>Each schema gets its own model cache entry</description></item>
/// <item><description>Model operations work correctly for the intended schema</description></item>
/// <item><description>Cross-schema contamination is prevented</description></item>
/// <item><description>Performance benefits of model caching are maintained</description></item>
/// </list>
/// </remarks>
/// <param name="context">
/// The <see cref="DbContext"/> instance associated with this cache key. The schema name is extracted from this context
/// if it's a <see cref="WorkflowDbContext"/> instance.
/// </param>
/// <param name="designTime">
/// A flag indicating whether this cache key is for design-time operations (like migrations) or runtime operations.
/// </param>
class CoreModelCacheKey(DbContext context, bool designTime) : ModelCacheKey(context, designTime)
{
    /// <summary>
    /// The database schema name extracted from the context, or "public" as the default schema if none is specified.
    /// This field is used in equality comparisons and hash code generation to ensure schema-specific cache separation.
    /// </summary>
    readonly string? _schema = (context as WorkflowDbContext)?.SchemaName ?? "public";
    
    /// <summary>
    /// The design-time flag indicating whether this cache key is for design-time or runtime operations.
    /// This field is included in equality comparisons to ensure proper cache separation between different operation modes.
    /// </summary>
    readonly bool _designTime = designTime;
    
    /// <summary>
    /// Determines whether this cache key is equal to another cache key by comparing base properties, schema name, and design-time flag.
    /// This method ensures that cache keys are considered equal only when all relevant properties match, including the schema information.
    /// </summary>
    /// <param name="other">
    /// The other <see cref="ModelCacheKey"/> to compare with this instance. Must be a <see cref="CoreModelCacheKey"/> 
    /// for schema-specific comparison to be performed.
    /// </param>
    /// <returns>
    /// <c>true</c> if the specified cache key is equal to this cache key (including schema and design-time matching);
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The equality comparison includes:
    /// <list type="bullet">
    /// <item><description>Base ModelCacheKey equality (from parent class)</description></item>
    /// <item><description>Schema name comparison</description></item>
    /// <item><description>Design-time flag comparison</description></item>
    /// </list>
    /// 
    /// This ensures that different schemas or design-time modes result in different cache entries,
    /// preventing model cache contamination across schemas.
    /// </remarks>
    protected override bool Equals(ModelCacheKey other)
    {
        return base.Equals(other)
               && other is CoreModelCacheKey otherKey
               && otherKey._schema == _schema
               && otherKey._designTime == _designTime;
    }

    /// <summary>
    /// Generates a hash code for this cache key by combining the base hash code with the schema name hash.
    /// This method ensures that cache keys for different schemas produce different hash codes for efficient cache lookup.
    /// </summary>
    /// <returns>
    /// An integer hash code that uniquely identifies this cache key based on the base properties and schema name.
    /// </returns>
    /// <remarks>
    /// The hash code is computed by combining:
    /// <list type="bullet">
    /// <item><description>The base ModelCacheKey hash code</description></item>
    /// <item><description>The schema name hash code</description></item>
    /// </list>
    /// 
    /// This ensures efficient cache lookups while maintaining the property that equal objects have equal hash codes,
    /// and different schemas are likely to have different hash codes for optimal cache distribution.
    /// </remarks>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), _schema);
    }
}