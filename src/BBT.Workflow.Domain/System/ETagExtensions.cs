namespace System;

/// <summary>
/// Extension methods for ETag operations
/// </summary>
public static class ETagExtensions
{
    /// <summary>
    /// Determines if the provided ETag matches the current ETag value.
    /// This is typically used for conditional HTTP requests (If-None-Match header).
    /// </summary>
    /// <param name="currentETag">The current ETag value to compare against</param>
    /// <param name="ifNoneMatch">The ETag value from the If-None-Match header</param>
    /// <returns>True if the ETags match, false otherwise</returns>
    public static bool MatchesIfNoneMatch(this string currentETag, string ifNoneMatch)
    {
        return currentETag.Equals(ifNoneMatch, StringComparison.OrdinalIgnoreCase);
    }
} 