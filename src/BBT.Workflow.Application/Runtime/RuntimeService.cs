using System.Text.Json;
using BBT.Aether.MultiSchema;
using BBT.Workflow.Instances;
using BBT.Workflow.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Runtime;

public sealed class RuntimeService(
    IInstanceRepository instanceRepository,
    ICurrentSchema currentSchema,
    IOptions<RuntimeOptions> runtimeOptions,
    IRuntimeInfoProvider runtimeInfoProvider,
    ILogger<RuntimeService> logger) : IRuntimeService
{
    public async Task<IEnumerable<T?>> GetAsync<T>(CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter
    {
        var schemaName = runtimeOptions.Value.GetSchemaNameByType(typeof(T));
        var schemaInfo = runtimeOptions.Value.Schemas[schemaName];

        using (currentSchema.Use(schemaInfo.Schema))
        {
            var results = await instanceRepository.GetActiveDataListAsync(cancellationToken);
            
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
                        logger.InstanceDeserializationFailed(ex, schemaName, item.Instance.Key, item.InstanceData.Version);
                        return null;
                    }
                    catch (Exception ex)
                    {
                        logger.InstanceDeserializationFailed(ex, schemaName, item.Instance.Key, item.InstanceData.Version);
                        return null;
                    }
                })
                .Where(flow => flow != null)
                .ToList();
            return flows;
        }
    }

    public async Task<T?> GetAsync<T>(string key, string version,
        CancellationToken cancellationToken = default) where T : class, IDomainEntity, IReferenceSetter
    {
        var schemaName = runtimeOptions.Value.GetSchemaNameByType(typeof(T));
        var schemaInfo = runtimeOptions.Value.Schemas[schemaName];
        using (currentSchema.Use(schemaInfo.Schema))
        {
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
                logger.InstanceDeserializationFailed(ex, schemaName, key, version);
                return null;
            }
            catch (Exception ex)
            {
                logger.InstanceDeserializationFailed(ex, schemaName, key, version);
                return null;
            }
        }
    }
}