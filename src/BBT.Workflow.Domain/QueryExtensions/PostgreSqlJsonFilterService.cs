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
        
        // Handle nested JSON paths (e.g., "parent.child.field")
        var jsonPath = BuildJsonPath(sanitizedField);
        
        return operatorType.ToLower() switch
        {
            "eq" => BuildEqualsCondition(jsonPath, value, jsonColumnName, parameters, ref parameterIndex),
            "ne" => BuildNotEqualsCondition(jsonPath, value, jsonColumnName, parameters, ref parameterIndex),
            "gt" => BuildNumericCondition(jsonPath, value, ">", jsonColumnName, parameters, ref parameterIndex),
            "ge" => BuildNumericCondition(jsonPath, value, ">=", jsonColumnName, parameters, ref parameterIndex),
            "lt" => BuildNumericCondition(jsonPath, value, "<", jsonColumnName, parameters, ref parameterIndex),
            "le" => BuildNumericCondition(jsonPath, value, "<=", jsonColumnName, parameters, ref parameterIndex),
            "between" => BuildBetweenCondition(jsonPath, value, jsonColumnName, parameters, ref parameterIndex),
            "match" or "like" => BuildLikeCondition(jsonPath, value, jsonColumnName, parameters, ref parameterIndex),
            "startswith" => BuildStartsWithCondition(jsonPath, value, jsonColumnName, parameters, ref parameterIndex),
            "endswith" => BuildEndsWithCondition(jsonPath, value, jsonColumnName, parameters, ref parameterIndex),
            "in" => BuildInCondition(jsonPath, value, jsonColumnName, parameters, ref parameterIndex),
            "nin" => BuildNotInCondition(jsonPath, value, jsonColumnName, parameters, ref parameterIndex),
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

    private static string BuildJsonPath(string field)
    {
        // Convert "parent.child.field" to PostgreSQL JSON path format
        // PostgreSQL requires double quotes around field names in JSON paths
        if (field.Contains('.'))
        {
            var parts = field.Split('.');
            return string.Join(",", parts.Select(p => $"\"{p}\""));
        }
        return $"\"{field}\"";
    }
    
    private static string BuildSimpleFieldPath(string field)
    {
        // For simple field access with ->> operator, just return the field name
        // Nested paths not supported with ->> (use #>> instead)
        if (field.Contains('.'))
        {
            throw new ArgumentException($"Nested paths not supported for this operation: {field}. Use simple field names only.");
        }
        return field;
    }

    private static (string, List<NpgsqlParameter>) BuildEqualsCondition(
        string jsonPath, string value, string jsonColumnName, 
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        // Use @> JSON containment operator for better performance (can use indexes)
        var conditions = new List<string>();
        
        // Convert single field path to proper JSON field name for containment
        var fieldName = jsonPath.Trim('"'); // Remove quotes from jsonPath
        
        // String comparison - use JSON containment @>
        var stringIndex = parameterIndex++;
        var stringJsonPattern = $"{{\"{fieldName}\":\"{value}\"}}";
        parameters.Add(new NpgsqlParameter { Value = stringJsonPattern, NpgsqlDbType = NpgsqlDbType.Jsonb });
        conditions.Add($"\"{jsonColumnName}\" @> {{{stringIndex}}}");
        
        // Numeric comparison (if value is numeric)
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numValue))
        {
            var numIndex = parameterIndex++;
            var numJsonPattern = $"{{\"{fieldName}\":{numValue}}}";
            parameters.Add(new NpgsqlParameter { Value = numJsonPattern, NpgsqlDbType = NpgsqlDbType.Jsonb });
            conditions.Add($"\"{jsonColumnName}\" @> {{{numIndex}}}");
        }
        
        // Boolean comparison (if value is boolean)
        if (bool.TryParse(value, out var boolValue))
        {
            var boolIndex = parameterIndex++;
            var boolJsonPattern = $"{{\"{fieldName}\":{boolValue.ToString().ToLower()}}}";
            parameters.Add(new NpgsqlParameter { Value = boolJsonPattern, NpgsqlDbType = NpgsqlDbType.Jsonb });
            conditions.Add($"\"{jsonColumnName}\" @> {{{boolIndex}}}");
        }
        
        var condition = $"({string.Join(" OR ", conditions)})";
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildNotEqualsCondition(
        string jsonPath, string value, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        // Use NOT @> JSON containment operator for better performance
        var conditions = new List<string>();
        
        // Convert single field path to proper JSON field name for containment
        var fieldName = jsonPath.Trim('"'); // Remove quotes from jsonPath
        
        // String comparison - use NOT JSON containment
        var stringIndex = parameterIndex++;
        var stringJsonPattern = $"{{\"{fieldName}\":\"{value}\"}}";
        parameters.Add(new NpgsqlParameter { Value = stringJsonPattern, NpgsqlDbType = NpgsqlDbType.Jsonb });
        conditions.Add($"NOT (\"{jsonColumnName}\" @> {{{stringIndex}}})");
        
        // Numeric comparison (if value is numeric)
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numValue))
        {
            var numIndex = parameterIndex++;
            var numJsonPattern = $"{{\"{fieldName}\":{numValue}}}";
            parameters.Add(new NpgsqlParameter { Value = numJsonPattern, NpgsqlDbType = NpgsqlDbType.Jsonb });
            conditions.Add($"NOT (\"{jsonColumnName}\" @> {{{numIndex}}})");
        }
        
        // Boolean comparison (if value is boolean) 
        if (bool.TryParse(value, out var boolValue))
        {
            var boolIndex = parameterIndex++;
            var boolJsonPattern = $"{{\"{fieldName}\":{boolValue.ToString().ToLower()}}}";
            parameters.Add(new NpgsqlParameter { Value = boolJsonPattern, NpgsqlDbType = NpgsqlDbType.Jsonb });
            conditions.Add($"NOT (\"{jsonColumnName}\" @> {{{boolIndex}}})");
        }
        
        // For not equals, we need ALL patterns to not match (AND logic)
        var condition = $"({string.Join(" AND ", conditions)})";
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildNumericCondition(
        string jsonPath, string value, string sqlOperator, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numValue))
        {
            throw new ArgumentException($"Value '{value}' is not numeric for comparison operator '{sqlOperator}'");
        }

        var paramIndex = parameterIndex++;
        parameters.Add(new NpgsqlParameter { Value = numValue });
        
        // Convert jsonPath to simple field name for ->> operator
        var fieldName = jsonPath.Trim('"'); // Remove quotes: "clientId" -> clientId
        
        // PostgreSQL native numeric comparison using ->> for single field access
        var condition = $"(\"{jsonColumnName}\" ->> '{fieldName}')::numeric {sqlOperator} {{{paramIndex}}}";
        
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildBetweenCondition(
        string jsonPath, string value, string jsonColumnName,
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
        
        // Convert jsonPath to simple field name for ->> operator
        var fieldName = jsonPath.Trim('"'); // Remove quotes: "clientId" -> clientId
        
        var condition = $"(\"{jsonColumnName}\" ->> '{fieldName}')::numeric BETWEEN {{{minIndex}}} AND {{{maxIndex}}}";
        
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildLikeCondition(
        string jsonPath, string value, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        var paramIndex = parameterIndex++;
        parameters.Add(new NpgsqlParameter { Value = $"%{value}%" });
        
        // Convert jsonPath to simple field name for ->> operator
        var fieldName = jsonPath.Trim('"'); // Remove quotes: "clientId" -> clientId
        
        var condition = $"(\"{jsonColumnName}\" ->> '{fieldName}') ILIKE {{{paramIndex}}}";
        
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildStartsWithCondition(
        string jsonPath, string value, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        var paramIndex = parameterIndex++;
        parameters.Add(new NpgsqlParameter { Value = $"{value}%" });
        
        // Convert jsonPath to simple field name for ->> operator
        var fieldName = jsonPath.Trim('"'); // Remove quotes: "clientId" -> clientId
        
        var condition = $"(\"{jsonColumnName}\" ->> '{fieldName}') ILIKE {{{paramIndex}}}";
        
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildEndsWithCondition(
        string jsonPath, string value, string jsonColumnName,
        List<NpgsqlParameter> parameters, ref int parameterIndex)
    {
        var paramIndex = parameterIndex++;
        parameters.Add(new NpgsqlParameter { Value = $"%{value}" });
        
        // Convert jsonPath to simple field name for ->> operator
        var fieldName = jsonPath.Trim('"'); // Remove quotes: "clientId" -> clientId
        
        var condition = $"(\"{jsonColumnName}\" ->> '{fieldName}') ILIKE {{{paramIndex}}}";
        
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildInCondition(
        string jsonPath, string value, string jsonColumnName,
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
        
        // Convert jsonPath to simple field name for ->> operator
        var fieldName = jsonPath.Trim('"'); // Remove quotes: "clientId" -> clientId
        
        var condition = $"(\"{jsonColumnName}\" ->> '{fieldName}') IN ({string.Join(", ", paramPlaceholders)})";
        
        return (condition, parameters);
    }

    private static (string, List<NpgsqlParameter>) BuildNotInCondition(
        string jsonPath, string value, string jsonColumnName,
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
        
        // Convert jsonPath to simple field name for ->> operator
        var fieldName = jsonPath.Trim('"'); // Remove quotes: "clientId" -> clientId
        
        var condition = $"(\"{jsonColumnName}\" ->> '{fieldName}') IS NOT NULL AND " +
                       $"(\"{jsonColumnName}\" ->> '{fieldName}') NOT IN ({string.Join(", ", paramNames)})";
        
        return (condition, parameters);
    }
} 