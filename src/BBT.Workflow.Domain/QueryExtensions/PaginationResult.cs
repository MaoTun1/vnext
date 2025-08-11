using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace BBT.Workflow.Definitions;
public class PaginationResult<T>
{
    public List<T> Data { get; set; }
    public PaginationLinks Pagination { get; set; }
}

public class PaginationLinks
{
    public string Self { get; set; }
    public string First { get; set; }
    public string Next { get; set; }
    public string Prev { get; set; }
}

public interface ILinkBuilder
{
    string BuildPageLink(int pageNumber, int pageSize, IDictionary<string, string>? queryParams = null);
}

public class LinkBuilder : ILinkBuilder
{
    private readonly string _baseUrl;
    private readonly string _route;

    public LinkBuilder(IConfiguration configuration, string route)
    {
        var baseUrl = configuration["vNextApi:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
        var apiVersion = configuration["vNextApi:ApiVersion"] ?? "1";
        _baseUrl = $"{baseUrl}/api/v{apiVersion}";
        _route = route.TrimStart('/');
    }

    public string BuildPageLink(int pageNumber, int pageSize, IDictionary<string, string>? queryParams = null)
    {
        var queryString = new List<string>
        {
            $"page={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (queryParams != null)
        {
            foreach (var param in queryParams)
            {
                if (param.Key != "page" && param.Key != "pageSize")
                {
                    queryString.Add($"{param.Key}={param.Value}");
                }
            }
        }

        return $"{_baseUrl}/{_route}?{string.Join("&", queryString)}";
    }
}
