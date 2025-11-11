using System.Text.Json;
using BBT.Aether.Application.Services;
using BBT.Aether.Http;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Retrieves a single instance with optional extensions
    /// GET {baseUrl}/api/v{version}/{domain}/workflows/{workflow}/instances/{instance}
    /// </summary>
    public async Task<ConditionalResult<GetInstanceOutput>> GetInstanceAsync(
        GetInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        try
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
                return ConditionalResult<GetInstanceOutput>.NotModified();
            }

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<GetInstanceOutput>(responseContent, JsonOptions);
                return ConditionalResult<GetInstanceOutput>.Success(result!);
            }

            var error = await HandleErrorResponse(response, cancellationToken);
            return ConditionalResult<GetInstanceOutput>.Fail(error);
        }
        catch (Exception ex)
        {
            var error = ex is InvalidOperationException
                ? Error.Dependency("remote_request_failed", ex.Message)
                : Error.Dependency("unexpected", ex.Message);
            return ConditionalResult<GetInstanceOutput>.Fail(error);
        }
    }

    /// <summary>
    /// Retrieves a paginated list of instances with optional extensions
    /// GET {baseUrl}/api/v{version}/{domain}/workflows/{workflow}/instances
    /// </summary>
    public async Task<Result<PaginationResult<GetInstanceOutput>>> GetInstanceListAsync(
        GetInstanceListInput input,
        CancellationToken cancellationToken = default)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
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

                var response = await httpClient.GetAsync(url, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<PaginationResult<GetInstanceOutput>>(responseContent, JsonOptions);
                    return result!;
                }

                var error = await HandleErrorResponse(response, ct);
                throw new InvalidOperationException($"Request failed: {error.Message}");
            },
            cancellationToken,
            ex => ex is InvalidOperationException
                ? Error.Dependency("remote_request_failed", ex.Message)
                : Error.Dependency("unexpected", ex.Message));
    }

    /// <summary>
    /// Retrieves the complete history of an instance (all data transitions)
    /// GET {baseUrl}/api/v{version}/{domain}/workflows/{workflow}/instances/{instance}/transitions
    /// </summary>
    public async Task<Result<GetInstanceHistoryOutput>> GetInstanceHistoryAsync(
        GetInstanceHistoryInput input,
        CancellationToken cancellationToken = default)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
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

                var response = await httpClient.GetAsync(url, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<GetInstanceHistoryOutput>(responseContent, JsonOptions);
                    return result!;
                }

                var error = await HandleErrorResponse(response, ct);
                throw new InvalidOperationException($"Request failed: {error.Message}");
            },
            cancellationToken,
            ex => ex is InvalidOperationException
                ? Error.Dependency("remote_request_failed", ex.Message)
                : Error.Dependency("unexpected", ex.Message));
    }

    /// <summary>
    /// Retrieves function result for an instance (e.g., "state" function returns GetInstanceStateOutput)
    /// GET {baseUrl}/api/v{version}/{domain}/workflows/{workflow}/instances/{instance}/functions/{function}
    /// </summary>
    public async Task<Result<GetInstanceStateOutput>> GetFunctionWithStateAsync(
        GetFunctionWithInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                var url =
                    $"/api/v{_options.ApiVersion}/{input.Domain}/workflows/{input.Workflow}/instances/{input.Instance}/functions/{Definitions.Functions.FunctionTypeConst.Longpooling}";

                var queryParams = new List<string>();
                if (!string.IsNullOrEmpty(input.Version))
                {
                    queryParams.Add($"{nameof(input.Version).ToLowerInvariant()}={Uri.EscapeDataString(input.Version)}");
                }

                if (input.Extensions?.Length > 0)
                {
                    foreach (var ext in input.Extensions)
                    {
                        queryParams.Add($"{nameof(input.Extensions).ToLowerInvariant()}={Uri.EscapeDataString(ext)}");
                    }
                }

                if (queryParams.Count > 0)
                    url += "?" + string.Join("&", queryParams);

                var response = await httpClient.GetAsync(url, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<GetInstanceStateOutput>(responseContent, JsonOptions);
                    return result!;
                }

                var error = await HandleErrorResponse(response, ct);
                throw new InvalidOperationException($"Request failed: {error.Message}");
            },
            cancellationToken,
            ex => ex is InvalidOperationException
                ? Error.Dependency("remote_request_failed", ex.Message)
                : Error.Dependency("unexpected", ex.Message));
    }

    /// <summary>
    /// Retrieves view function result for an instance (returns GetViewOutput with platform-specific content)
    /// GET {baseUrl}/api/v{version}/{domain}/workflows/{workflow}/instances/{instance}/functions/view
    /// </summary>
    public async Task<Result<GetViewOutput>> GetFunctionWithViewAsync(
        GetFunctionWithInstanceInput input,
        string? platform,
        string? transitionKey,
        CancellationToken cancellationToken = default)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                var url =
                    $"/api/v{_options.ApiVersion}/{input.Domain}/workflows/{input.Workflow}/instances/{input.Instance}/functions/{Definitions.Functions.FunctionTypeConst.View}";

                var queryParams = new List<string>();
                if (!string.IsNullOrEmpty(input.Version))
                {
                    queryParams.Add($"{nameof(input.Version).ToLowerInvariant()}={Uri.EscapeDataString(input.Version)}");
                }

                if (!string.IsNullOrEmpty(platform))
                {
                    queryParams.Add($"{nameof(platform)}={Uri.EscapeDataString(platform)}");
                }
                
                if (!string.IsNullOrEmpty(transitionKey))
                {
                    queryParams.Add($"{nameof(transitionKey)}={Uri.EscapeDataString(transitionKey)}");
                }

                if (input.Extensions?.Length > 0)
                {
                    foreach (var ext in input.Extensions)
                    {
                        queryParams.Add($"{nameof(input.Extensions).ToLowerInvariant()}={Uri.EscapeDataString(ext)}");
                    }
                }

                if (queryParams.Count > 0)
                    url += "?" + string.Join("&", queryParams);

                var response = await httpClient.GetAsync(url, ct);

                if (response.IsSuccessStatusCode)
                {
                    // The endpoint returns only the Content as a string (not a full GetViewOutput object)
                    // So we need to wrap it in GetViewOutput structure
                    var responseContent = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<GetViewOutput>(responseContent, JsonOptions);
                    return result!;
                }

                var error = await HandleErrorResponse(response, ct);
                throw new InvalidOperationException($"Request failed: {error.Message}");
            },
            cancellationToken,
            ex => ex is InvalidOperationException
                ? Error.Dependency("remote_request_failed", ex.Message)
                : Error.Dependency("unexpected", ex.Message));
    }

    /// <summary>
    /// Handles error responses by converting to Error
    /// </summary>
    private static async Task<Error> HandleErrorResponse(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var statusCode = (int)response.StatusCode;

        // Check if response has Aether error format header
        if (response.Headers.TryGetValues("_bbt_error_format", out var values) && 
            values.Any(v => v.Equals("true", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ServiceErrorResponse>(errorContent, JsonOptions);
                if (errorResponse?.Error != null)
                {
                    // Convert ServiceErrorInfo to Error
                    return new Error(
                        errorResponse.Error.Code ?? "remote_error",
                        errorResponse.Error.Message,
                        errorResponse.Error.Details);
                }
            }
            catch (JsonException)
            {
                // If JSON deserialization fails, fall back to the default behavior
            }
        }

        // Default behavior for non-Aether format or deserialization failures
        return Error.Dependency("remote_http_error", 
            $"HTTP {statusCode}: {response.ReasonPhrase}", 
            target: statusCode.ToString());
    }
}