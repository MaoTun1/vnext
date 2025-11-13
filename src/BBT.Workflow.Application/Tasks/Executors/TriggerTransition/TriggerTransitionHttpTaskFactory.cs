using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Execution.TriggerTransition;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks.TriggerTransition;

/// <summary>
/// Factory implementation for creating HTTP tasks used in trigger transition strategies.
/// </summary>
public sealed class TriggerTransitionHttpTaskFactory : ITriggerTransitionHttpTaskFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TriggerTransitionHttpTaskFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerTransitionHttpTaskFactory"/> class.
    /// </summary>
    /// <param name="configuration">The configuration instance for reading vNextApi settings.</param>
    /// <param name="logger">The logger instance.</param>
    public TriggerTransitionHttpTaskFactory(
        IConfiguration configuration,
        ILogger<TriggerTransitionHttpTaskFactory> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public HttpTask CreateHttpTask(
        TriggerTransitionTask triggerTask,
        ScriptContext context,
        string path,
        string method)
    {
        // Build full URL using configuration
        var baseUrl = _configuration["vNextApi:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
        var apiVersion = _configuration["vNextApi:ApiVersion"] ?? "1";
        var fullUrl = $"{baseUrl}/api/v{apiVersion}{path}";

        _logger.LogDebug("Creating HttpTask with URL: {Url}", fullUrl);

        // Prepare body from triggerTask.Body or context.Body
        JsonElement? body = triggerTask.Body;
        if (!body.HasValue && context.Body != null)
        {
            body = JsonSerializer.SerializeToElement(context.Body);
        }

        // Prepare headers from context.Headers
        var headersDict = new Dictionary<string, string>();
        if (context.Headers != null)
        {
            foreach (var header in context.Headers)
            {
                var key = header.Key?.ToString() ?? string.Empty;
                var value = header.Value?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    headersDict[key] = value;
                }
            }
        }

        // Get timeout from configuration
        var timeoutSeconds = _configuration.GetValue<int>("vNextApi:TimeoutSeconds", 30);

        // Create task config JSON
        var configBuilder = new Dictionary<string, object>
        {
            ["key"] = triggerTask.Key,
            ["url"] = fullUrl,
            ["method"] = method,
            ["timeoutSeconds"] = timeoutSeconds,
            ["validateSSL"] = true
        };

        if (headersDict.Count > 0)
        {
            configBuilder["headers"] = headersDict;
        }

        if (body.HasValue)
        {
            configBuilder["body"] = body.Value;
        }

        var configJson = JsonSerializer.Serialize(configBuilder);
        var configElement = JsonDocument.Parse(configJson).RootElement;

        var httpTask = HttpTask.Create(configElement);

        // Copy base properties from triggerTask
        triggerTask.CopyBaseToInternal(httpTask);

        return httpTask;
    }
}

