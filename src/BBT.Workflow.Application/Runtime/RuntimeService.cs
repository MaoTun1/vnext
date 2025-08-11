using System.Text.Json;
using BBT.Aether.DistributedCache;
using BBT.Workflow.Instances;
using BBT.Workflow.Schemas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Runtime;

public sealed class RuntimeService(
    IInstanceRepository instanceRepository,
    ICurrentSchema currentSchema,
    ISchemaManager schemaManager,
    IOptions<RuntimeOptions> runtimeOptions,
    IRuntimeInfoProvider runtimeInfoProvider,
    IDistributedCacheService distributedCache,
    ILogger<RuntimeService> logger) : IRuntimeService
{
    private const string SchemaExistsCacheKeyPrefix = "schema_exists_";
    private static readonly TimeSpan SchemaExistsCacheDuration = TimeSpan.FromDays(1);

    /// <summary>
    /// Checks if a schema exists with caching to improve performance in read-only scenarios.
    /// Only caches positive results (schema exists = true) to avoid stale negative caches
    /// when schemas are created after the initial check.
    /// </summary>
    /// <param name="schemaName">The name of the schema to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if schema exists, false otherwise</returns>
    private async Task<bool> CheckSchemaExistsWithCacheAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{SchemaExistsCacheKeyPrefix}{schemaName}";

        // Try to get from distributed cache - only check for true values
        var cachedValue = await distributedCache.GetAsync<string>(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedValue) && bool.TryParse(cachedValue, out bool cachedResult) && cachedResult)
        {
            logger.LogDebug("Schema existence check cache hit for schema: {Schema} - schema exists", schemaName);
            return true;
        }

        logger.LogDebug("Schema existence check cache miss for schema: {Schema}. Querying database.", schemaName);
        var exists = await schemaManager.SchemaExistsAsync(schemaName, cancellationToken);

        // Only cache positive results (schema exists = true)
        // This prevents stale negative caches when schemas are created later
        if (exists)
        {
            var cacheOptions = new DistributedCacheEntryOptions
            {
                SlidingExpiration = SchemaExistsCacheDuration
            };

            await distributedCache.SetAsync(cacheKey, "true", cacheOptions, cancellationToken);
            logger.LogDebug("Cached positive schema existence result for schema: {Schema}", schemaName);
        }
        else
        {
            logger.LogDebug("Schema '{Schema}' does not exist - not caching negative result", schemaName);
        }

        return exists;
    }

    public async Task<IEnumerable<T?>> GetAsync<T>(string schema, CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter
    {
        var schemaInfo = runtimeOptions.Value.Schemas[schema];

        using (currentSchema.Change(schemaInfo.Schema))
        {
            if (runtimeOptions.Value.EnableSchemaMigration)
            {
                await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);
            }
            else
            {
                // In read-only mode, check if schema exists before proceeding (with caching)
                if (!await CheckSchemaExistsWithCacheAsync(currentSchema.Name, cancellationToken))
                {
                    logger.LogWarning(
                        "Schema '{Schema}' does not exist and schema migration is disabled. Returning empty collection.",
                        currentSchema.Name);
                    return [];
                }
            }

            List<InstanceAndDataModel> results;
            try
            {
                results = await instanceRepository.GetActiveDataListAsync(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error getting active data list");
                return [];
            }

            var flows = results
                .Select(item =>
                {
                    try
                    {
                        var flow = item.InstanceData.Data.JsonElement
                            .Deserialize<T>(JsonSerializerConstants.JsonOptions);

                        if (flow != null)
                        {
                            flow.SetReference(new Reference(
                                item.Instance.Key ?? string.Empty,
                                runtimeInfoProvider.Domain,
                                schemaInfo.Name,
                                item.InstanceData.Version
                            ));
                        }

                        return flow;
                    }
                    catch (JsonException ex)
                    {
                        logger.LogError(ex,
                            "Failed to deserialize workflow instance data. Schema: {Schema}, InstanceKey: {InstanceKey}, Version: {Version}, JsonData: {JsonData}",
                            schema, item.Instance.Key, item.InstanceData.Version, item.InstanceData.Data.JsonElement.GetRawText());

                        return null;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Unexpected error during workflow instance deserialization. Schema: {Schema}, InstanceKey: {InstanceKey}, Version: {Version}",
                            schema, item.Instance.Key, item.InstanceData.Version);

                        return null;
                    }
                })
                .Where(flow => flow != null)
                .ToList();
            return flows;
        }
    }

    public async Task<T?> GetAsync<T>(string schema, string key, string version,
        CancellationToken cancellationToken = default) where T : class, IDomainEntity, IReferenceSetter
    {
        var schemaInfo = runtimeOptions.Value.Schemas[schema];
        using (currentSchema.Change(schemaInfo.Schema))
        {
            if (runtimeOptions.Value.EnableSchemaMigration)
            {
                await schemaManager.EnsureSchemaAndTablesAsync(currentSchema.Name, cancellationToken);
            }
            else
            {
                // In read-only mode, check if schema exists before proceeding (with caching)
                if (!await CheckSchemaExistsWithCacheAsync(currentSchema.Name, cancellationToken))
                {
                    logger.LogWarning(
                        "Schema '{Schema}' does not exist and schema migration is disabled. Returning null for key '{Key}', version '{Version}'.",
                        currentSchema.Name, key, version);
                    return null;
                }
            }

            var item = await instanceRepository.FindActiveDataAsync(key, version, cancellationToken);

            if (item == null)
            {
                return null;
            }

            try
            {
                var flow = item.InstanceData.Data.JsonElement
                    .Deserialize<T>(JsonSerializerConstants.JsonOptions);

                if (flow != null)
                {
                    flow.SetReference(new Reference(
                        item.Instance.Key ?? string.Empty,
                        runtimeInfoProvider.Domain,
                        schemaInfo.Name,
                        item.InstanceData.Version
                    ));
                }

                return flow;
            }
            catch (JsonException ex)
            {
                logger.LogError(ex,
                    "Failed to deserialize specific workflow instance data. Schema: {Schema}, InstanceKey: {InstanceKey}, Version: {Version}, JsonData: {JsonData}",
                    schema, key, version, item.InstanceData.Data.JsonElement.GetRawText());

                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Unexpected error during specific workflow instance deserialization. Schema: {Schema}, InstanceKey: {InstanceKey}, Version: {Version}",
                    schema, key, version);

                return null;
            }
        }
    }
}