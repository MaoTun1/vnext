using System.Text;
using System.Text.Json;
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

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Starts a new workflow instance by calling the remote API
    /// POST {baseUrl}/api/v{version}/{domain}/workflows/{workflow}/instances/start
    /// </summary>
    public async Task<InstanceServiceResponse<StartInstanceOutput>> StartAsync(
        StartInstanceInput input,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v{_options.ApiVersion}/{input.Domain}/workflows/{input.Workflow}/instances/start";

        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(input.Version))
            queryParams.Add($"version={Uri.EscapeDataString(input.Version)}");
        if (input.Sync)
            queryParams.Add("sync=true");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        var requestBody = new CreateInstanceInput
        {
            Key = input.Instance.Key,
            Tags = input.Instance.Tags,
            Attributes = input.Instance.Attributes
        };

        var jsonContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
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

        var response = await httpClient.SendAsync(requestMessage, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<StartInstanceOutput>(responseContent, _jsonOptions);
            return new InstanceServiceResponse<StartInstanceOutput>(result!);
        }

        await HandleErrorResponse(response, cancellationToken);
        throw new InvalidOperationException("Request failed without throwing an exception");
    }

    /// <summary>
    /// Executes a transition on an existing workflow instance
    /// PATCH {baseUrl}/api/v{version}/{domain}/workflows/{workflow}/instances/{instanceId}/transitions/{transitionKey}
    /// </summary>
    public async Task<InstanceServiceResponse<TransitionOutput>> TransitionAsync(
        Guid instanceId,
        string transitionKey,
        TransitionInput input,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"api/v{_options.ApiVersion}/{input.Domain}/workflows/{input.Workflow}/instances/{instanceId}/transitions/{transitionKey}";

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

        var response = await httpClient.SendAsync(requestMessage, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<TransitionOutput>(responseContent, _jsonOptions);
            return new InstanceServiceResponse<TransitionOutput>(result!);
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

        throw new HttpRequestException($"HTTP {statusCode}: {response.ReasonPhrase}. Content: {errorContent}");
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