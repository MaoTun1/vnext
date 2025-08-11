using BBT.Workflow.Definitions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

public static class QueryExtensions
{
    public static PaginationResult<T> Paginate<T>(
        this IQueryable<T> query, 
        int page, 
        int pageSize, 
        string route,
        IConfiguration configuration,
        IQueryCollection? queryParams = null) where T : class
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 10;

        var skip = (page - 1) * pageSize;
        
        // Fetch one extra item to check if there's a next page
        var items = query.Skip(skip).Take(pageSize + 1).ToList();
        var hasNextPage = items.Count > pageSize;
        
        // Remove the extra item if it exists
        var data = hasNextPage ? items.Take(pageSize).ToList() : items;

        var linkBuilder = new LinkBuilder(configuration, route);
        var queryParamsDict = queryParams?.ToDictionary(x => x.Key, x => x.Value.ToString());

        var links = new PaginationLinks
        {
            Self = linkBuilder.BuildPageLink(page, pageSize, queryParamsDict),
            First = linkBuilder.BuildPageLink(1, pageSize, queryParamsDict),
            Next = hasNextPage ? linkBuilder.BuildPageLink(page + 1, pageSize, queryParamsDict) : string.Empty,
            Prev = page > 1 ? linkBuilder.BuildPageLink(page - 1, pageSize, queryParamsDict) : string.Empty
        };

        return new PaginationResult<T>
        {
            Data = data,
            Pagination = links
        };
    }
}