using System.Dynamic;
using System.Text.Json;
using BBT.Workflow.Definitions;
using BBT.Workflow.Scripting;
using Microsoft.Extensions.Logging;

namespace BBT.Workflow.Tasks;

/// <summary>
/// Executes HTTP-based workflow tasks that make external web service calls.
/// This executor handles HTTP requests with custom headers, body content, and response processing.
/// </summary>
/// <param name="scriptEngine">The script engine used for compiling input/output mapping scripts.</param>
/// <param name="httpClientFactory">The HTTP client factory for creating HTTP clients to make web requests.</param>
/// <param name="logger">The logger instance for logging HTTP task execution details.</param>
public sealed class HttpTaskExecutor(
    IScriptEngine scriptEngine,
    IHttpClientFactory httpClientFactory,
    ILogger<HttpTaskExecutor> logger) : TaskExecutor(scriptEngine, logger), ITaskExecutor
{
    /// <summary>
    /// Named HttpClient for SSL validation enabled requests (default behavior)
    /// </summary>
    public const string DefaultHttpClientName = "WorkflowHttpClient";

    /// <summary>
    /// Named HttpClient for SSL validation disabled requests
    /// </summary>
    public const string NoSslValidationHttpClientName = "WorkflowHttpClient.NoSslValidation";
    /// <summary>
    /// Executes an HTTP task by preparing the request through script mapping, making the HTTP call,
    /// and processing the response through output mapping.
    /// </summary>
    /// <param name="task">The HTTP workflow task containing URL, method, and configuration details.</param>
    /// <param name="scriptCode">The script code that handles input preparation and output processing.</param>
    /// <param name="context">The script context containing data for request preparation and response handling.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the processed response data
    /// from the HTTP call after output mapping transformation.
    /// </returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or returns an error status code.</exception>
    /// <exception cref="InvalidCastException">Thrown when the task cannot be cast to HttpTask type.</exception>
    public async Task<object?> ExecuteAsync(
        WorkflowTask task,
        string scriptCode,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var httpTask = (task as HttpTask)!;
        
        try
        {
            await PrepareInputAsync(httpTask, scriptCode, context, cancellationToken);
            await CallAsync(httpTask, context, cancellationToken);
            
            var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);
            return outputResponse;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred during HTTP task {TaskKey} execution", httpTask.Key);
            throw;
        }
    }

    /// <summary>
    /// Executes an HTTP call without script processing. Used for internal workflow operations.
    /// This method makes the HTTP request and sets the response in the context.
    /// </summary>
    /// <param name="task">The HTTP workflow task to execute.</param>
    /// <param name="context">The script context for storing the response.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during execution.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task CallAsync(
        WorkflowTask task,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var httpTask = (task as HttpTask)!;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        StandardTaskResponse standardResponse;

        try
        {
            var httpClient = CreateHttpClient(httpTask);
            var request = new HttpRequestMessage(new HttpMethod(httpTask.Method), httpTask.Url);
           if (httpTask.Headers.HasValue)
            {
                var headers = httpTask.Headers.Value.Deserialize<Dictionary<string, string>>();
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }
            
            if (request.Method != HttpMethod.Get && httpTask.Body.HasValue)
            {
                var requestContent = httpTask.Body.Value.GetRawText();
                request.Content = new StringContent(
                    requestContent,
                    System.Text.Encoding.UTF8,
                    "application/json"
                );
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            // Extract response headers
            var responseHeaders = response.Headers
                .Concat(response.Content.Headers)
                .ToDictionary(h => h.Key.ToLower(), h => string.Join(", ", h.Value));

            var content = await response.ReadDecompressedContentAsync(cancellationToken);
            object? responseData = null;
            
            if (!content.IsNullOrEmpty())
            {
                try
                {
                    responseData = JsonSerializer.Deserialize<object>(content);
                }
                catch (JsonException)
                {
                    responseData = content;
                }
            }

            // Create standardized response based on HTTP status
            if (response.IsSuccessStatusCode)
            {
                standardResponse = CreateSuccessResponse(
                    data: responseData,
                    taskType: nameof(TaskType.Http),
                    executionDurationMs: stopwatch.ElapsedMilliseconds,
                    statusCode: (int)response.StatusCode,
                    headers: responseHeaders,
                    metadata: new Dictionary<string, object>
                    {
                        ["Url"] = httpTask.Url,
                        ["Method"] = httpTask.Method,
                        ["ReasonPhrase"] = response.ReasonPhrase ?? string.Empty
                    });
            }
            else
            {
                standardResponse = CreateErrorResponse(
                    errorMessage: $"HTTP request failed with status {response.StatusCode}: {response.ReasonPhrase}",
                    taskType: nameof(TaskType.Http),
                    executionDurationMs: stopwatch.ElapsedMilliseconds,
                    statusCode: (int)response.StatusCode,
                    metadata: new Dictionary<string, object>
                    {
                        ["Url"] = httpTask.Url,
                        ["Method"] = httpTask.Method,
                        ["ReasonPhrase"] = response.ReasonPhrase ?? string.Empty,
                        ["ResponseContent"] = content
                    });
            }
            
            context.SetStandardResponse(standardResponse);
            context.SetBody(responseData);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
                
            standardResponse = CreateErrorResponse(
                errorMessage: "HTTP request was cancelled",
                taskType: nameof(TaskType.Http),
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["Url"] = httpTask.Url,
                    ["Method"] = httpTask.Method,
                    ["Cancelled"] = true
                });
            
            context.SetStandardResponse(standardResponse);
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "HTTP request failed for task {TaskKey} - URL: {Url}, Method: {Method}, Duration: {Duration}ms", 
                httpTask.Key, httpTask.Url, httpTask.Method, stopwatch.ElapsedMilliseconds);
                
            standardResponse = CreateErrorResponse(
                errorMessage: ex.Message,
                taskType: nameof(TaskType.Http),
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["Url"] = httpTask.Url,
                    ["Method"] = httpTask.Method
                });
            
            context.SetStandardResponse(standardResponse);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Unexpected error occurred during HTTP task {TaskKey} execution after {Duration}ms", 
                httpTask.Key, stopwatch.ElapsedMilliseconds);
                
            standardResponse = CreateErrorResponse(
                errorMessage: ex.Message,
                taskType: nameof(TaskType.Http),
                executionDurationMs: stopwatch.ElapsedMilliseconds,
                exception: ex,
                metadata: new Dictionary<string, object>
                {
                    ["Url"] = httpTask.Url,
                    ["Method"] = httpTask.Method
                });
            
            context.SetStandardResponse(standardResponse);
            throw;
        }
    }

    /// <summary>
    /// Creates an HttpClient using the appropriate named client based on SSL validation settings.
    /// This method leverages HttpClientFactory for connection pooling and proper disposal.
    /// </summary>
    /// <param name="httpTask">The HTTP task containing SSL validation and timeout configuration.</param>
    /// <returns>A configured HttpClient instance from the factory pool.</returns>
    private HttpClient CreateHttpClient(HttpTask httpTask)
    {
        string clientName;
        
        if (!httpTask.ValidateSSL)
        {
            Logger.LogWarning("SSL certificate validation is disabled for HTTP task {TaskKey} - URL: {Url}", 
                httpTask.Key, httpTask.Url);
            clientName = NoSslValidationHttpClientName;
        }
        else
        {
            clientName = DefaultHttpClientName;
        }

        // Get HttpClient from factory using named client
        var httpClient = httpClientFactory.CreateClient(clientName);
        
        // Override timeout for this specific request
        httpClient.Timeout = TimeSpan.FromSeconds(httpTask.TimeoutSeconds);
            
        return httpClient;
    }
}