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
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        Logger.LogInformation("Starting HTTP task execution for task {TaskKey} - URL: {Url}, Method: {Method}", 
            httpTask.Key, httpTask.Url, httpTask.Method);
            
        StandardTaskResponse standardResponse;

        try
        {
            Logger.LogDebug("Preparing input for HTTP task {TaskKey}", httpTask.Key);
            var inputResponse = await PrepareInputAsync(httpTask, scriptCode, context, cancellationToken);
            
            var httpClient = httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(new HttpMethod(httpTask.Method), httpTask.Url);

            Logger.LogDebug("Created HTTP request for {Method} {Url}", httpTask.Method, httpTask.Url);

            if (httpTask.Headers.HasValue)
            {
                var headers = httpTask.Headers.Value.Deserialize<Dictionary<string, string>>();
                if (headers != null)
                {
                    Logger.LogDebug("Adding {HeaderCount} headers to HTTP request for task {TaskKey}", 
                        headers.Count, httpTask.Key);
                    
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        Logger.LogDebug("Added header {HeaderKey}: {HeaderValue} to request", header.Key, header.Value);
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
                
                Logger.LogDebug("Added request body to HTTP request for task {TaskKey}", httpTask.Key);
            }

            Logger.LogInformation("Sending HTTP request for task {TaskKey}: {Method} {Url}", 
                httpTask.Key, httpTask.Method, httpTask.Url);
                
            var response = await httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            Logger.LogInformation("HTTP request completed for task {TaskKey} - Status: {StatusCode}, Duration: {Duration}ms", 
                httpTask.Key, response.StatusCode, stopwatch.ElapsedMilliseconds);

            // Extract response headers
            var responseHeaders = response.Headers
                .Concat(response.Content.Headers)
                .ToDictionary(h => h.Key.ToLower(), h => string.Join(", ", h.Value));

            Logger.LogDebug("Extracted {HeaderCount} response headers from HTTP response", responseHeaders.Count);

            var content = await response.ReadDecompressedContentAsync(cancellationToken);
            object? responseData = null;
            
            if (!content.IsNullOrEmpty())
            {
                try
                {
                    responseData = JsonSerializer.Deserialize<object>(content);
                    Logger.LogDebug("Successfully deserialized response content to object for task {TaskKey}", httpTask.Key);
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning(ex, "Failed to deserialize response content as JSON for task {TaskKey}, treating as raw string", httpTask.Key);
                    responseData = content;
                }
            }

            // Create standardized response based on HTTP status
            if (response.IsSuccessStatusCode)
            {
                Logger.LogInformation("HTTP task {TaskKey} completed successfully with status {StatusCode}", 
                    httpTask.Key, response.StatusCode);
                    
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
                Logger.LogWarning("HTTP task {TaskKey} returned non-success status {StatusCode}: {ReasonPhrase}", 
                    httpTask.Key, response.StatusCode, response.ReasonPhrase);
                    
                standardResponse = CreateErrorResponse(
                    errorMessage: $"HTTP request failed with status {response.StatusCode}: {response.ReasonPhrase}",
                    taskType: "HttpTask",
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
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            Logger.LogWarning("HTTP task {TaskKey} was cancelled after {Duration}ms", 
                httpTask.Key, stopwatch.ElapsedMilliseconds);
                
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
        }

        Logger.LogDebug("Setting standard response in context for HTTP task {TaskKey}", httpTask.Key);
        context.SetStandardResponse(standardResponse);
        
        Logger.LogDebug("Processing output for HTTP task {TaskKey}", httpTask.Key);
        var outputResponse = await ProcessOutputAsync(scriptCode, context, cancellationToken);
        
        Logger.LogInformation("HTTP task {TaskKey} execution completed, returning processed output", httpTask.Key);
        return outputResponse.Data;
    }
}