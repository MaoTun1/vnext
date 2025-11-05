using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Application.Notifications;

public sealed class DaprComponentDetector(
    DaprClient daprClient,
    IConfiguration configuration,
    ILogger<DaprComponentDetector> logger)
{
    public async Task<(string ComponentType, NotificationComponentType NotificationType, Dictionary<string, string> Metadata)> DetectAsync(
        string componentName,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        // Try to detect component type from Dapr runtime metadata first
        var componentType = await TryResolveComponentTypeFromDaprAsync(componentName, cancellationToken) ?? string.Empty;

        NotificationComponentType notificationType = componentType switch
        {
            var t when !string.IsNullOrWhiteSpace(t) && t.StartsWith("pubsub.", StringComparison.OrdinalIgnoreCase)
                => NotificationComponentType.PubSub,
            var t when !string.IsNullOrWhiteSpace(t) && t.Equals("bindings.http", StringComparison.OrdinalIgnoreCase)
                => NotificationComponentType.HttpBinding,
            var t when !string.IsNullOrWhiteSpace(t) && t.Equals("bindings.mqtt", StringComparison.OrdinalIgnoreCase)
                => NotificationComponentType.MqttBinding,
            _ => NotificationComponentType.HttpBinding // Default to HTTP binding
        };

        // If pubsub or mqtt and topic missing, inject default topic from configuration if present
        if ((notificationType == NotificationComponentType.PubSub ||
             notificationType == NotificationComponentType.MqttBinding) &&
            !metadata.ContainsKey("topic"))
        {
            var cfgTopic = configuration[$"DaprComponents:{componentName}:topic"];
            if (!string.IsNullOrWhiteSpace(cfgTopic))
            {
                metadata["topic"] = cfgTopic!;
            }
        }

        // Note: method and ForwardingHeaders are read from NotificationTask.Metadata in NotificationTaskExecutor
        // and already included in the metadata dictionary passed here

        return (componentType, notificationType, metadata);
    }

    private async Task<string?> TryResolveComponentTypeFromDaprAsync(string componentName, CancellationToken cancellationToken)
    {
        try
        {
            var md = await daprClient.GetMetadataAsync(cancellationToken);
            // Serialize to JSON and search for components array fields without relying on SDK property names
            var json = JsonSerializer.Serialize(md);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Common property names observed: "registeredComponents", "components", "Components"
            if (root.ValueKind == JsonValueKind.Object)
            {
                // Try different property name variations (case-insensitive search)
                JsonElement? componentArray = null;
                if (root.TryGetProperty("registeredComponents", out var reg) && reg.ValueKind == JsonValueKind.Array)
                {
                    componentArray = reg;
                }
                else if (root.TryGetProperty("components", out var comps) && comps.ValueKind == JsonValueKind.Array)
                {
                    componentArray = comps;
                }
                else if (root.TryGetProperty("Components", out var compsCap) && compsCap.ValueKind == JsonValueKind.Array)
                {
                    componentArray = compsCap;
                }

                if (componentArray.HasValue)
                {
                    var type = FindTypeInArray(componentArray.Value, componentName);
                    if (!string.IsNullOrWhiteSpace(type)) return type;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed resolve component type from dapr component");

            return null;
        }
        return null;
    }

    private static string? FindTypeInArray(JsonElement array, string componentName)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            // Try both lowercase and uppercase property names
            string? name = null;
            if (item.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
            {
                name = nameEl.GetString();
            }
            else if (item.TryGetProperty("Name", out var nameElCap) && nameElCap.ValueKind == JsonValueKind.String)
            {
                name = nameElCap.GetString();
            }

            if (name != null && string.Equals(name, componentName, StringComparison.OrdinalIgnoreCase))
            {
                // Try both lowercase and uppercase property names for type
                if (item.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                {
                    return typeEl.GetString();
                }
                if (item.TryGetProperty("Type", out var typeElCap) && typeElCap.ValueKind == JsonValueKind.String)
                {
                    return typeElCap.GetString();
                }
            }
        }
        return null;
    }


}

