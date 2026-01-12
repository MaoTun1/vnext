using System.Text.Json.Serialization;
using BBT.Aether;
using BBT.Aether.Domain.Repositories;

namespace BBT.Workflow.Instances;

/// <summary>
/// Response for instance list queries with optional groups
/// When groupBy is used, items array contains GroupSummary objects instead of instance items
/// </summary>
/// <typeparam name="T">Item type (typically GetInstanceOutput)</typeparam>
public sealed class InstanceListWithGroupsResponse<T>
{
    /// <summary>
    /// HATEOAS links for pagination
    /// </summary>
    [JsonPropertyName("links")]
    public object? Links { get; set; }

    /// <summary>
    /// List of items (instances or groups when groupBy is used)
    /// </summary>
    [JsonPropertyName("items")]
    public List<object> Items { get; set; } = new();

    /// <summary>
    /// Creates response from HateoasPagedList
    /// </summary>
    public static InstanceListWithGroupsResponse<T> FromPagedList(HateoasPagedList<T> pagedList, List<GroupSummary>? groups = null)
    {
        // If groups are provided, populate items with groups; otherwise use paged list items
        if (groups != null && groups.Count > 0)
        {
            return new InstanceListWithGroupsResponse<T>
            {
                Links = null, // Links will be set by Controller using linkGenerator.CreateHateoasResult
                Items = groups.Cast<object>().ToList()
            };
        }

        return new InstanceListWithGroupsResponse<T>
        {
            Links = null, // Links will be set by Controller using linkGenerator.CreateHateoasResult
            Items = pagedList.Items.Cast<object>().ToList()
        };
    }

    /// <summary>
    /// Creates response from groups directly
    /// </summary>
    public static InstanceListWithGroupsResponse<T> FromGroups(List<GroupSummary> groups)
    {
        return new InstanceListWithGroupsResponse<T>
        {
            Links = null, // Links will be set by Controller using linkGenerator.CreateHateoasResult
            Items = groups.Cast<object>().ToList()
        };
    }

    /// <summary>
    /// Converts to HateoasPagedList for backward compatibility
    /// </summary>
    public HateoasPagedList<T> ToPagedList(int pageSize)
    {
        // Estimate hasNext based on items count (this is approximate)
        var hasNext = Items.Count > pageSize;
        var typedItems = Items.OfType<T>().ToList();
        return new HateoasPagedList<T>(typedItems, 1, typedItems.Count, hasNext);
    }
}

