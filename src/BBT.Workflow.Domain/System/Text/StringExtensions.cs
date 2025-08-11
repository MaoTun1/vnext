using System.Text.RegularExpressions;

namespace System.Text;

/// <summary>
/// Extension methods for string operations in Instance Extensions
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Converts a string with hyphens and other special characters to a valid variable name format.
    /// Examples: "user-info" -> "userInfo", "create-user-task" -> "createUserTask"
    /// </summary>
    /// <param name="key">The key string to convert</param>
    /// <returns>Variable name formatted string</returns>
    public static string ToVariableName(this string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return key;

        // Replace hyphens and underscores with spaces, then convert to camelCase
        var words = Regex.Split(key, @"[-_\s]+", RegexOptions.IgnoreCase)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToArray();

        if (words.Length == 0)
            return key;

        // First word lowercase, subsequent words title case
        var result = words[0].ToLowerInvariant();
        for (int i = 1; i < words.Length; i++)
        {
            result += char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();
        }

        // Ensure it starts with a letter or underscore (valid variable name)
        if (!char.IsLetter(result[0]) && result[0] != '_')
        {
            result = "_" + result;
        }

        return result;
    }
}