using System.Text;
using System.Text.Json;
using BBT.Aether.Http;
using BBT.Workflow.Definitions;
using BBT.Workflow.Domain;
using BBT.Workflow.ExceptionHandling;
using BBT.Workflow.Remote.Configuration;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Instances.Remote;

/// <summary>
/// Remote implementation of instance command operations using HTTP client calls to InstanceController
/// </summary>
public sealed class RemoteInstanceCommandAppService(
    HttpClient httpClient,
    IOptions<RemoteOptions> options)
    : IRemoteInstanceCommandAppService
{
    private readonly RemoteOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Starts a new workflow instance by calling the remote API
    /// POST {baseUrl}/api/v{version}/{domain}/workflows/{workflow}/instances/start
    /// </summary>
    public async Task<Result<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                var url = $"api/v{_options.ApiVersion}" + string.Format(InstanceUrlTemplates.Start, input.Domain, input.Workflow);

                var queryParams = new List<string>();
                if (!string.IsNullOrEmpty(input.Version))
                    queryParams.Add($"version={Uri.EscapeDataString(input.Version)}");
                if (input.Sync)
                    queryParams.Add($"sync={input.Sync}");

                if (queryParams.Count > 0)
                    url += "?" + string.Join("&", queryParams);

                var requestBody = new CreateInstanceInput
                {
                    Key = input.Instance.Key,
                    Tags = input.Instance.Tags,
                    Attributes = input.Instance.Attributes
                };

                var jsonContent = JsonSerializer.Serialize(requestBody, JsonOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };

                if (input.Headers != null)
                    foreach (var header in input.Headers)
                    {
                        if (!IsRestrictedHeader(header.Key))
                        {
                            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }

                var response = await httpClient.SendAsync(requestMessage, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<StartInstanceOutput>(responseContent, JsonOptions);
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
    /// Starts a new sub workflow instance by calling the remote API
    /// POST {baseUrl}/api/v{version}/{domain}/workflows/sub/{workflow}/instances/start
    /// </summary>
    public async Task<Result<StartInstanceOutput>> StartSubAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                var url = $"api/v{_options.ApiVersion}" + string.Format(InstanceUrlTemplates.StartSub, input.Domain, input.Workflow);

                var queryParams = new List<string>();
                if (!string.IsNullOrEmpty(input.Version))
                    queryParams.Add($"version={Uri.EscapeDataString(input.Version)}");
                if (input.Sync)
                    queryParams.Add($"sync={input.Sync}");

                if (queryParams.Count > 0)
                    url += "?" + string.Join("&", queryParams);

                var requestBody = new CreateInstanceInput
                {
                    Id = input.Instance.Id,
                    Key = input.Instance.Key,
                    Tags = input.Instance.Tags,
                    Attributes = input.Instance.Attributes,
                    Callback = input.Instance.Callback,
                    MetaData = input.Instance.MetaData
                };

                var jsonContent = JsonSerializer.Serialize(requestBody, JsonOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };

                if (input.Headers != null)
                    foreach (var header in input.Headers)
                    {
                        if (!IsRestrictedHeader(header.Key))
                        {
                            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }

                var response = await httpClient.SendAsync(requestMessage, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<StartInstanceOutput>(responseContent, JsonOptions);
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
    /// Executes a transition on an existing workflow instance
    /// PATCH {baseUrl}/api/v{version}/{domain}/workflows/{workflow}/instances/{instanceId}/transitions/{transitionKey}
    /// </summary>
    public async Task<Result<TransitionOutput>> TransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        return await ResultExtensions.TryAsync(
            async ct =>
            {
                var url = $"api/v{_options.ApiVersion}" + string.Format(InstanceUrlTemplates.Transition, input.Domain, input.Workflow, instanceId, transitionKey);

                var queryParams = new List<string>();
                if (!string.IsNullOrEmpty(input.Version))
                    queryParams.Add($"version={Uri.EscapeDataString(input.Version)}");
                if (input.Sync)
                    queryParams.Add("sync=true");

                if (queryParams.Count > 0)
                    url += "?" + string.Join("&", queryParams);

                var content = input.Data.HasValue
                    ? new StringContent(input.Data.Value.GetRawText(), Encoding.UTF8, "application/json")
                    : new StringContent("null", Encoding.UTF8, "application/json");

                var requestMessage = new HttpRequestMessage(HttpMethod.Patch, url)
                {
                    Content = content
                };

                foreach (var header in input.Headers)
                {
                    if (!IsRestrictedHeader(header.Key))
                    {
                        requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                var response = await httpClient.SendAsync(requestMessage, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<TransitionOutput>(responseContent, JsonOptions);
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

    /// <summary>
    /// Checks if a header is restricted and cannot be set manually
    /// </summary>
    private static bool IsRestrictedHeader(string headerName)
    {
        return headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Connection", StringComparison.OrdinalIgnoreCase);
    }
}