using System.Text.Json;
using BBT.Aether.Application.Services;
using BBT.Aether.Http;
using BBT.Workflow.Definitions;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Remote.Configuration;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Instances.Remote;

/// <summary>
/// Remote implementation of instance query operations using HTTP client calls to InstanceController
/// </summary>
public sealed class RemoteInstanceQueryAppService(
    HttpClient httpClient,
    IOptions<RemoteOptions> options)
    : IRemoteInstanceQueryAppService
{
    private readonly RemoteOptions _options = options.Value;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Retrieves a single instance with optional extensions
    /// GET {baseUrl}/api/v{version}/{domain}/workflows/{workflow}/instances/{instance}
    /// </summary>
    public async Task<InstanceServiceResult<GetInstanceOutput>> GetInstanceAsync(
        GetInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v{_options.ApiVersion}/{input.Domain}/workflows/{input.Workflow}/instances/{input.Instance}";

        var queryParams = new List<string>();
        if (input.Extension?.Length > 0)
        {
            foreach (var ext in input.Extension)
            {
                queryParams.Add($"extension={Uri.EscapeDataString(ext)}");
            }
        }

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

        // Add If-None-Match header for ETag support
        if (!string.IsNullOrEmpty(input.IfNoneMatch))
        {
            requestMessage.Headers.TryAddWithoutValidation("If-None-Match", input.IfNoneMatch);
        }

        var response = await httpClient.SendAsync(requestMessage, cancellationToken);

        // Handle 304 Not Modified
        if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
        {
            return InstanceServiceResult<GetInstanceOutput>.NotModified();
        }

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GetInstanceOutput>(responseContent, _jsonOptions);
            return InstanceServiceResult<GetInstanceOutput>.Success(result!);
        }

        await HandleErrorResponse(response, cancellationToken);
        throw new InvalidOperationException("Request failed without throwing an exception");
    }

    /// <summary>
    /// Retrieves a paginated list of instances with optional extensions
    /// GET {baseUrl}/api/v{version}/{domain}/workflows/{workflow}/instances
    /// </summary>
    public async Task<InstanceServiceResponse<PaginationResult<GetInstanceOutput>>> GetInstanceListAsync(
        GetInstanceListInput input,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v{_options.ApiVersion}/{input.Domain}/workflows/{input.Workflow}/instances";

        var queryParams = new List<string>
        {
            $"page={input.Page}",
            $"pageSize={input.PageSize}"
        };

        if (input.Extension?.Length > 0)
        {
            foreach (var ext in input.Extension)
            {
                queryParams.Add($"extension={Uri.EscapeDataString(ext)}");
            }
        }

        url += "?" + string.Join("&", queryParams);

        var response = await httpClient.GetAsync(url, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<PaginationResult<GetInstanceOutput>>(responseContent, _jsonOptions);
            return new InstanceServiceResponse<PaginationResult<GetInstanceOutput>>(result!);
        }

        await HandleErrorResponse(response, cancellationToken);
        throw new InvalidOperationException("Request failed without throwing an exception");
    }

    /// <summary>
    /// Retrieves the complete history of an instance (all data transitions)
    /// GET {baseUrl}/api/v{version}/{domain}/workflows/{workflow}/instances/{instance}/transitions
    /// </summary>
    public async Task<InstanceServiceResponse<GetInstanceHistoryOutput>> GetInstanceHistoryAsync(
        GetInstanceHistoryInput input,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"/api/v{_options.ApiVersion}/{input.Domain}/workflows/{input.Workflow}/instances/{input.Instance}/transitions";

        var queryParams = new List<string>();
        if (input.Extension?.Length > 0)
        {
            foreach (var ext in input.Extension)
            {
                queryParams.Add($"extension={Uri.EscapeDataString(ext)}");
            }
        }

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        var response = await httpClient.GetAsync(url, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GetInstanceHistoryOutput>(responseContent, _jsonOptions);
            return new InstanceServiceResponse<GetInstanceHistoryOutput>(result!);
        }

        await HandleErrorResponse(response, cancellationToken);
        throw new InvalidOperationException("Request failed without throwing an exception");
    }

    public async Task<InstanceServiceResponse<GetAvailableTransitionOutput>> GetAvailableTransitionsAsync(
        GetAvailableTransitionInput input, CancellationToken cancellationToken = default)
    {
        var url =
            $"/api/v{_options.ApiVersion}/{input.Domain}/workflows/{input.Workflow}/instances/{input.Instance}transitions/available";

        var queryParams = new List<string>();
        if (!input.Version.IsNullOrEmpty())
        {
            queryParams.Add($"version={Uri.EscapeDataString(input.Version)}");
        }

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        
        var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GetAvailableTransitionOutput>(responseContent, _jsonOptions);
            return new InstanceServiceResponse<GetAvailableTransitionOutput>(result!);
        }

        await HandleErrorResponse(response, cancellationToken);
        throw new InvalidOperationException("Request failed without throwing an exception");
    }

    /// <summary>
    /// Handles error responses by throwing appropriate exceptions
    /// </summary>
    private static async Task HandleErrorResponse(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var statusCode = (int)response.StatusCode;

        // Check if response has Aether error format header
        if (response.Headers.TryGetValues("_bbt_error_format", out var values) && 
            values.Any(v => v.Equals("true", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var errorResponse = JsonSerializer.Deserialize<ServiceErrorResponse>(errorContent, jsonOptions);
                if (errorResponse?.Error != null)
                {
                    throw new RemoteServiceException(
                        $"Remote service error: {errorResponse.Error.Message}", 
                        errorResponse.Error, 
                        statusCode);
                }
            }
            catch (JsonException)
            {
                // If JSON deserialization fails, fall back to the default behavior
            }
        }

        // Default behavior for non-Aether format or deserialization failures
        throw new HttpRequestException($"HTTP {statusCode}: {response.ReasonPhrase}. Content: {errorContent}");
    }
}