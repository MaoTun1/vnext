namespace BBT.Workflow.Instances;

public class InstanceDataVersionComparer : IComparer<InstanceData>
{
    public static InstanceDataVersionComparer Instance { get; } = new();
    
    public int Compare(InstanceData? x, InstanceData? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        return CompareVersionStrings(x.Version, y.Version);
    }

    private static int CompareVersionStrings(string? v1, string? v2)
    {
        if (string.IsNullOrWhiteSpace(v1) && string.IsNullOrWhiteSpace(v2)) return 0;
        if (string.IsNullOrWhiteSpace(v1)) return -1;
        if (string.IsNullOrWhiteSpace(v2)) return 1;

        if (Version.TryParse(v1, out var version1) && Version.TryParse(v2, out var version2))
        {
            return version1.CompareTo(version2);
        }

        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase); // fallback
    }
}