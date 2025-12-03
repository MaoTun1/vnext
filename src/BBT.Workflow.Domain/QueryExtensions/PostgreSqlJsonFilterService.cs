using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace BBT.Workflow.Definitions;

/// <summary>
/// PostgreSQL native JSONB filter service using FromSqlRaw for optimal performance
/// Supports all numeric operations without range limitations
/// </summary>
public static class PostgreSqlJsonFilterService
{
    /// <summary>
    /// Apply JSON filters using PostgreSQL native JSONB operators with CTE approach
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="dbSet">Entity DbSet</param>
    /// <param name="filters">Array of filter strings in format: "field=operator:value"</param>
    /// <param name="jsonColumnName">Name of the JSON column (e.g., "Data", "Json")</param>
    /// <param name="tableName">Name of the database table</param>
    /// <param name="schema">Database schema name</param>
    /// <returns>Filtered queryable</returns>
    public static IQueryable<T> ApplyJsonFilters<T>(
        this DbSet<T> dbSet,
        string[] filters,
        string jsonColumnName = "Data",
        string tableName = "",
        string schema = "public") where T : class
    {
        if (filters == null || !filters.Any())
            return dbSet;

        var whereConditions = new List<string>();
        var parameters = new List<NpgsqlParameter>();
        var parameterIndex = 0;

        foreach (var filter in filters)
        {
            try
            {
                var (field, operatorType, operatorValue) = FilterOperatorParser.ParseOperator(filter);
                
                var (condition, filterParameters) = BuildPostgreSqlCondition(
                    field, operatorType, operatorValue, jsonColumnName, ref parameterIndex);
                
                if (!string.IsNullOrEmpty(condition))
                {
                    whereConditions.Add(condition);
                    parameters.AddRange(filterParameters);
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with other filters
                Console.WriteLine($"Error parsing filter '{filter}': {ex.Message}");
            }
        }

        if (!whereConditions.Any())
            return dbSet;

        // Get table name if not provided
        if (string.IsNullOrEmpty(tableName))
        {
            tableName = typeof(T).Name + "s"; // Default convention: Entity + "s"
        }

        // Build CTE-based SQL query - working pattern format
        var whereClause = string.Join(" AND ", whereConditions);
        
        // Schema is not parameterized - use string interpolation like working pattern
        var rawSql = $@"
            WITH FilteredData AS (
                SELECT DISTINCT ON (""InstanceId"") 
                    ""Id"",
                    ""InstanceId"",
                    ""Data"",
                    ""EnteredAt"",
                    ""IsLatest""
                FROM ""{schema}"".""InstancesData""
                WHERE ""IsLatest"" = true AND ({whereClause})
                ORDER BY ""InstanceId"", ""EnteredAt"" DESC
            )
            SELECT s.*
            FROM ""{schema}"".""Instances"" s
            JOIN FilteredData d ON s.""Id"" = d.""InstanceId""
            ORDER BY s.""CreatedAt"" DESC";

        // Use exact working pattern - NpgsqlParameter array without @ in SQL
        return dbSet.FromSqlRaw(rawSql, parameters.ToArray()).AsNoTracking();
    }

    /// <summary>
    /// Apply single JSON filter using PostgreSQL native JSONB operators
    /// </summary>
    public static IQueryable<T> ApplyJsonFilter<T>(
        this DbSet<T> dbSet,
        string field,
        string operatorType,
        string value,
        string jsonColumnName = "Json",
        string tableName = "") where T : class
    {
        return dbSet.ApplyJsonFilters(
            new[] { $"{field}={operatorType}:{value}" },
            jsonColumnName,
            tableName);
    }

    /// <summary>
    /// Create filtered SQL query string with parameters (for manual execution)
    /// </summary>
    public static (string sql, NpgsqlParameter[] parameters) BuildFilteredQuery<T>(
        string[] filters,
        string jsonColumnName = "Json",
        string tableName = "") where T : class
    {
        if (filters == null || !filters.Any())
            return ("", Array.Empty<NpgsqlParameter>());

        var whereConditions = new List<string>();
        var parameters = new List<NpgsqlParameter>();
        var parameterIndex = 0;

        foreach (var filter in filters)
        {
            try
            {
                var (field, operatorType, operatorValue) = FilterOperatorParser.ParseOperator(filter);
                
                var (condition, filterParameters) = BuildPostgreSqlCondition(
                    field, operatorType, operatorValue, jsonColumnName, ref parameterIndex);
                
                if (!string.IsNullOrEmpty(condition))
                {
                    whereConditions.Add(condition);
                    parameters.AddRange(filterParameters);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing filter '{filter}': {ex.Message}");
            }
        }

        if (!whereConditions.Any())
            return ("", Array.Empty<NpgsqlParameter>());

        if (string.IsNullOrEmpty(tableName))
        {
            tableName = typeof(T).Name + "s";
        }

        var whereClause = string.Join(" AND ", whereConditions);
        var sql = $"SELECT * FROM \"{tableName}\" WHERE {whereClause}";

        return (sql, parameters.ToArray());
    }

    private static (string condition, List<NpgsqlParameter> parameters) BuildPostgreSqlCondition(
        string field,
        string operatorType,
        string value,
        string jsonColumnName,
        ref int parameterIndex)
    {
        // Sanitize field name to prevent SQL injection
        var sanitizedField = SanitizeFieldName(field);
        var parameters = new List<NpgsqlParameter>();
        
        // Pass sanitizedField directly - condition builders will handle nested vs single level
        return operatorType.ToLower() switch
        {
            "eq" => BuildEqualsCondition(sanitizedField, value, jsonColumnName, parameters, ref parameterIndex),
            "ne" => BuildNotEqualsCondition(sanitizedField, value, jsonColumnName, parameters, ref parameterIndex),
            "gt" => BuildNumericCondition(sanitizedField, value, ">", jsonColumnName, parameters, ref parameterIndex),
            "ge" => BuildNumericCondition(sanitizedField, value, ">=", jsonColumnName, parameters, ref parameterIndex),
            "lt" => BuildNumericCondition(sanitizedField, value, "<", jsonColumnName, parameters, ref parameterIndex),
            "le" => BuildNumericCondition(sanitizedField, value, "<=", jsonColumnName, parameters, ref parameterIndex),
            "between" => BuildBetweenCondition(sanitizedField, value, jsonColumnName, parameters, ref parameterIndex),
            "match" or "like" => BuildLikeCondition(sanitizedField, value, jsonColumnName, parameters, ref parameterIndex),
            "startswith" => BuildStartsWithCondition(sanitizedField, value, jsonColumnName, parameters, ref parameterIndex),
            "endswith" => BuildEndsWithCondition(sanitizedField, value, jsonColumnName, parameters, ref parameterIndex),
            "in" => BuildInCondition(sanitizedField, value, jsonColumnName, parameters, ref parameterIndex),
            "nin" => BuildNotInCondition(sanitizedField, value, jsonColumnName, parameters, ref parameterIndex),
            _ => throw new ArgumentException($"Unsupported operator: {operatorType}")
        };
    }

    private static string SanitizeFieldName(string field)
    {
        // Only allow alphanumeric characters, dots, and underscores
        if (!System.Text.RegularExpressions.Regex.IsMatch(field, @"^[a-zA-Z0-9._]+$"))
        {
            throw new ArgumentException($"Invalid field name: {field}. Only alphanumeric, dots, and underscores allowed.");
        }
        return field;
    }


    /// <summary>
    /// Check if the field path is nested (contains dots)
    /// </summary>
    private static bool IsNestedPath(string field) => field.Contains('.');

    /// <summary>
    /// Build nested JSON containment pattern for @> operator
    /// Example: "parent.child" + "123" (numeric) -> {"parent":{"child":123}}
    /// Example: "parent.child" + "test" (string) -> {"parent":{"child":"test"}}
    /// </summary>
    private static string BuildNestedJsonContainmentPattern(string field, string value, bool isNumeric, bool isBoolean)
    {
        var parts = field.Split('.');
        
        // Build the innermost value
        string innerValue;
        if (isBoolean && bool.TryParse(value, out var boolVal))
        {
            innerValue = boolVal.ToString().ToLower();
        }
        else if (isNumeric && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numVal))
        {
            innerValue = numVal.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            innerValue = $"\"{value}\"";
        }
        
        // Build nested JSON from inside out
        // For "parent.child.field" with value "123", build: {"parent":{"child":{"field":123}}}
        var result = $"{{\"{parts[^1]}\":{innerValue}}}";
        
        for (int i = parts.Length - 2; i >= 0; i--)
        {
            result = $"{{\"{parts[i]}\":{result}}}";
        }
        
        return result;
    }

    /// <summary>
    /// Build JSON text accessor expression for PostgreSQL
    /// Single level: ("Data" ->> 'field')
    /// Nested: ("Data" #>> ARRAY['parent','child'])
    /// </summary>
    private static string BuildJsonTextAccessor(string field, string jsonColumnName)
    {
        if (IsNestedPath(field))
        {
            // Use #>> operator with ARRAY syntax for nested fields
            // ARRAY syntax avoids curly brace issues with FromSqlRaw parameter parsing
            var parts = field.Split('.');
            var arrayElements = string.Join(",", parts.Select(p => $"'{p}'"));
            return $"(\"{jsonColumnName}\" #>> ARRAY[{arrayElements}])";
        }
        else
        {
            // Use ->> operator for single level field (existing behavior)
            return $"(\"{jsonColumnName}\" ->> '{field}')";
        }
    }

    private static (string, List<NpgsqlParameter>) BuildEqualsCondition(
        string field, string value, string jsonColumnName, 
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        // Use @> JSON containment operator for better performance (can use indexes)
        var conditions = new List<string>();
        var isNested = IsNestedPath(field);
        
        // String comparison - use JSON containment @>
        var stringIndex = parameterIndex++;
        string stringJsonPattern;
        if (isNested)
        {
            // Build nested JSON pattern: {"parent":{"child":"value"}}
            stringJsonPattern = BuildNestedJsonContainmentPattern(field, value, isNumeric: false, isBoolean: false);
        }
        else
        {
            // Single level: {"field":"value"} (existing behavior)
            stringJsonPattern = $"{{\"{field}\":\"{value}\"}}";
        }
        parameters.Add(new NpgsqlParameter { Value = stringJsonPattern, NpgsqlDbType = NpgsqlDbType.Jsonb });
        conditions.Add($"\"{jsonColumnName}\" @> {{{stringIndex}}}");
        
        // Numeric comparison (if value is numeric)
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            var numIndex = parameterIndex++;
            string numJsonPattern;
            if (isNested)
            {
                numJsonPattern = BuildNestedJsonContainmentPattern(field, value, isNumeric: true, isBoolean: false);
            }
            else
            {
                var numValue = decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
                numJsonPattern = $"{{\"{field}\":{numValue}}}";
            }
            parameters.Add(new NpgsqlParameter { Value = numJsonPattern, NpgsqlDbType = NpgsqlDbType.Jsonb });
            conditions.Add($"\"{jsonColumnName}\" @> {{{numIndex}}}");
        }
        
        // Boolean comparison (if value is boolean)
        if (bool.TryParse(value, out _))
        {
            var boolIndex = parameterIndex++;
            string boolJsonPattern;
            if (isNested)
            {
                boolJsonPattern = BuildNestedJsonContainmentPattern(field, value, isNumeric: false, isBoolean: true);
            }
            else
            {
                var boolValue = bool.Parse(value);
                boolJsonPattern = $"{{\"{field}\":{boolValue.ToString().ToLower()}}}";
            }
            parameters.Add(new NpgsqlParameter { Value = boolJsonPattern, NpgsqlDbType = NpgsqlDbType.Jsonb });
            conditions.Add($"\"{jsonColumnName}\" @> {{{boolIndex}}}");
        }
        
        var condition = $"({string.Join(" OR ", conditions)})";
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildNotEqualsCondition(
        string field, string value, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        // Use NOT @> JSON containment operator for better performance
        var conditions = new List<string>();
        var isNested = IsNestedPath(field);
        
        // String comparison - use NOT JSON containment
        var stringIndex = parameterIndex++;
        string stringJsonPattern;
        if (isNested)
        {
            stringJsonPattern = BuildNestedJsonContainmentPattern(field, value, isNumeric: false, isBoolean: false);
        }
        else
        {
            stringJsonPattern = $"{{\"{field}\":\"{value}\"}}";
        }
        parameters.Add(new NpgsqlParameter { Value = stringJsonPattern, NpgsqlDbType = NpgsqlDbType.Jsonb });
        conditions.Add($"NOT (\"{jsonColumnName}\" @> {{{stringIndex}}})");
        
        // Numeric comparison (if value is numeric)
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            var numIndex = parameterIndex++;
            string numJsonPattern;
            if (isNested)
            {
                numJsonPattern = BuildNestedJsonContainmentPattern(field, value, isNumeric: true, isBoolean: false);
            }
            else
            {
                var numValue = decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
                numJsonPattern = $"{{\"{field}\":{numValue}}}";
            }
            parameters.Add(new NpgsqlParameter { Value = numJsonPattern, NpgsqlDbType = NpgsqlDbType.Jsonb });
            conditions.Add($"NOT (\"{jsonColumnName}\" @> {{{numIndex}}})");
        }
        
        // Boolean comparison (if value is boolean) 
        if (bool.TryParse(value, out _))
        {
            var boolIndex = parameterIndex++;
            string boolJsonPattern;
            if (isNested)
            {
                boolJsonPattern = BuildNestedJsonContainmentPattern(field, value, isNumeric: false, isBoolean: true);
            }
            else
            {
                var boolValue = bool.Parse(value);
                boolJsonPattern = $"{{\"{field}\":{boolValue.ToString().ToLower()}}}";
            }
            parameters.Add(new NpgsqlParameter { Value = boolJsonPattern, NpgsqlDbType = NpgsqlDbType.Jsonb });
            conditions.Add($"NOT (\"{jsonColumnName}\" @> {{{boolIndex}}})");
        }
        
        // For not equals, we need ALL patterns to not match (AND logic)
        var condition = $"({string.Join(" AND ", conditions)})";
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildNumericCondition(
        string field, string value, string sqlOperator, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numValue))
        {
            throw new ArgumentException($"Value '{value}' is not numeric for comparison operator '{sqlOperator}'");
        }

        var paramIndex = parameterIndex++;
        parameters.Add(new NpgsqlParameter { Value = numValue });
        
        // Use BuildJsonTextAccessor for proper nested/single level handling
        var accessor = BuildJsonTextAccessor(field, jsonColumnName);
        
        // PostgreSQL native numeric comparison
        var condition = $"{accessor}::numeric {sqlOperator} {{{paramIndex}}}";
        
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildBetweenCondition(
        string field, string value, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        var parts = value.Split(',');
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid between format: {value}. Expected format: 'min,max'");

        var minValue = parts[0].Trim();
        var maxValue = parts[1].Trim();
        
        if (!decimal.TryParse(minValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var minNum) ||
            !decimal.TryParse(maxValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var maxNum))
        {
            throw new ArgumentException($"Between values must be numeric: '{minValue}', '{maxValue}'");
        }

        var minIndex = parameterIndex++;
        var maxIndex = parameterIndex++;
        
        parameters.Add(new NpgsqlParameter { Value = minNum });
        parameters.Add(new NpgsqlParameter { Value = maxNum });
        
        // Use BuildJsonTextAccessor for proper nested/single level handling
        var accessor = BuildJsonTextAccessor(field, jsonColumnName);
        
        var condition = $"{accessor}::numeric BETWEEN {{{minIndex}}} AND {{{maxIndex}}}";
        
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildLikeCondition(
        string field, string value, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        var paramIndex = parameterIndex++;
        parameters.Add(new NpgsqlParameter { Value = $"%{value}%" });
        
        // Use BuildJsonTextAccessor for proper nested/single level handling
        var accessor = BuildJsonTextAccessor(field, jsonColumnName);
        
        var condition = $"{accessor} ILIKE {{{paramIndex}}}";
        
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildStartsWithCondition(
        string field, string value, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        var paramIndex = parameterIndex++;
        parameters.Add(new NpgsqlParameter { Value = $"{value}%" });
        
        // Use BuildJsonTextAccessor for proper nested/single level handling
        var accessor = BuildJsonTextAccessor(field, jsonColumnName);
        
        var condition = $"{accessor} ILIKE {{{paramIndex}}}";
        
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildEndsWithCondition(
        string field, string value, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        var paramIndex = parameterIndex++;
        parameters.Add(new NpgsqlParameter { Value = $"%{value}" });
        
        // Use BuildJsonTextAccessor for proper nested/single level handling
        var accessor = BuildJsonTextAccessor(field, jsonColumnName);
        
        var condition = $"{accessor} ILIKE {{{paramIndex}}}";
        
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildInCondition(
        string field, string value, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        var values = value.Split(',').Select(v => v.Trim()).ToArray();
        var paramPlaceholders = new List<string>();
        
        foreach (var val in values)
        {
            var paramIndex = parameterIndex++;
            parameters.Add(new NpgsqlParameter { Value = val });
            paramPlaceholders.Add($"{{{paramIndex}}}");
        }
        
        // Use BuildJsonTextAccessor for proper nested/single level handling
        var accessor = BuildJsonTextAccessor(field, jsonColumnName);
        
        var condition = $"{accessor} IN ({string.Join(", ", paramPlaceholders)})";
        
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildNotInCondition(
        string field, string value, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        var values = value.Split(',').Select(v => v.Trim()).ToArray();
        var paramNames = new List<string>();
        
        foreach (var val in values)
        {
            var paramIndex = parameterIndex++;
            parameters.Add(new NpgsqlParameter { Value = val });
            paramNames.Add($"{{{paramIndex}}}");
        }
        
        // Use BuildJsonTextAccessor for proper nested/single level handling
        var accessor = BuildJsonTextAccessor(field, jsonColumnName);
        
        var condition = $"{accessor} IS NOT NULL AND " +
                       $"{accessor} NOT IN ({string.Join(", ", paramNames)})";
        
        return (condition, parameters);
    }
} 