using System.Text;
using System.Text.Json;
using BBT.Workflow.Notifications;
using BBT.Workflow.Instances;
using BBT.Workflow.Caching;
using BBT.Workflow.Scripting;
using BBT.Workflow.Runtime;
using BBT.Workflow.Extentions;
using BBT.Workflow.Schemas;
using BBT.Workflow.Shared;
using BBT.Workflow.Definitions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Infrastructure.Notifications;

/// <summary>
/// Implementation of SignalR notification service
/// </summary>
public sealed class SignalRNotificationService(
    HttpClient httpClient,
    IConfiguration configuration,
    IInstanceRepository instanceRepository,
    IComponentCacheStore componentCacheStore,
    IScriptContextFactory scriptContextFactory,
    IRuntimeInfoProvider runtimeInfoProvider,
    IInstanceExtensionService instanceExtensionService,
    ICurrentSchema currentSchema,
    ILogger<SignalRNotificationService> logger) : ISignalRNotificationService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IConfiguration _configuration = configuration;
    private readonly IInstanceRepository _instanceRepository = instanceRepository;
    private readonly IComponentCacheStore _componentCacheStore = componentCacheStore;
    private readonly IScriptContextFactory _scriptContextFactory = scriptContextFactory;
    private readonly IRuntimeInfoProvider _runtimeInfoProvider = runtimeInfoProvider;
    private readonly IInstanceExtensionService _instanceExtensionService = instanceExtensionService;
    private readonly ICurrentSchema _currentSchema = currentSchema;
    private readonly ILogger<SignalRNotificationService> _logger = logger;

    /// <inheritdoc />
    public async Task SendWorkflowCompletedNotificationAsync(
        Guid instanceId,
        string domain,
        string workflow,
        Dictionary<string, string?>? headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var appId = _configuration["SignalR:AppId"];
            var method = _configuration["SignalR:Method"];

            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(method))
            {
                _logger.LogWarning("SignalR configuration is missing. AppId: {AppId}, Method: {Method}", appId, method);
                return;
            }

            // Get instance data to create GetInstanceOutput
            var instance = await _instanceRepository.FindAsync(instanceId, true, cancellationToken);
            if (instance == null)
            {
                _logger.LogWarning("Instance {InstanceId} not found for SignalR notification", instanceId);
                return;
            }

            // Get workflow for extension processing
            var flow = await _componentCacheStore.GetFlowAsync(domain, workflow, null, cancellationToken);

            // Create GetInstanceOutput from instance data
            var instanceOutput = new GetInstanceOutput
            {
                Id = instance.Id,
                Key = instance.Key,
                Flow = instance.Flow,
                Domain = domain,
                FlowVersion = instance.LatestData?.Version ?? string.Empty,
                Etag = instance.LatestData?.ETag ?? string.Empty,
                Tags = instance.Tags
            };

            // Process extensions for data enrichment (same as InstanceQueryAppService)
            var scriptContext = await _scriptContextFactory.NewBuilder()
                .WithWorkflow(flow)
                .WithInstance(instance)
                .WithRuntime(_runtimeInfoProvider)
                .WithTransition(string.Empty)
                .WithBody(instance.LatestData?.Data ?? new JsonData("{}"))
                .BuildAsync(cancellationToken);

            instanceOutput.Extensions = await _instanceExtensionService.ProcessExtensionsAsync(
                null, // No specific extensions requested, process all available
                scriptContext,
                flow,
                ExtensionScope.GetInstance,
                cancellationToken);
            var signalRRequest = new SignalRRequest
            {
                Id = instanceId.ToString(),
                Source = "vnext",
                Type = "vnext.workflow",
                Subject = "workflow-completed",
                Data = JsonSerializer.SerializeToElement(instanceOutput)
            };

            var jsonContent = JsonSerializer.Serialize(signalRRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var requestUrl = $"{appId}{method}";
            
            // Create HTTP request message to add headers
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = content
            };

            // Add headers from context if available
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (!string.IsNullOrEmpty(header.Key) && !string.IsNullOrEmpty(header.Value)&&!header.Key.Equals("content-type", StringComparison.OrdinalIgnoreCase) &&
                            !header.Key.Equals("content-length", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip content-type and other content headers as they are set by StringContent
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }
            
            _logger.LogInformation(
                "Sending SignalR notification for instance {InstanceId} to {RequestUrl} with {HeaderCount} headers",
                instanceId, requestUrl, headers?.Count ?? 0);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Successfully sent SignalR notification for instance {InstanceId}",
                    instanceId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to send SignalR notification for instance {InstanceId}. Status: {StatusCode}",
                    instanceId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending SignalR notification for instance {InstanceId}",
                instanceId);
        }
    }
}
