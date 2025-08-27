# Instance Filtering

## Overview

The BBT Workflow Engine provides powerful filtering capabilities for workflow instances using **PostgreSQL native JSONB operations**. The filtering system allows you to query instances based on their JSON attributes stored in the `InstanceData.Data` column with high performance and flexibility.

## Architecture

The filtering system is built on three main components:

```
┌─────────────────────┐    ┌──────────────────────┐    ┌─────────────────────┐
│   API Controller    │    │  Application Layer   │    │ PostgreSQL Native   │
│                     │    │                      │    │                     │
│ Query Parameters    │───▶│  Filter Processing   │───▶│   JSONB Operators   │
│ ?filter=attributes=field=op:val│    │                      │    │                     │
└─────────────────────┘    └──────────────────────┘    └─────────────────────┘
```

### Components

- **FilterOperatorParser**: Parses filter expressions from query parameters
- **PostgreSqlJsonFilterService**: Builds native PostgreSQL JSONB queries
- **InstanceRepository**: Executes optimized database queries with Common Table Expressions (CTE)

## Filter Syntax

### Basic Format

```
filter=attributes={field}={operator}:{value}
```

### Multiple Filters

```
?filter=attributes=clientId=eq:122&filter=attributes=testValue=gt:2
```

### URL Encoding

When using special characters, ensure proper URL encoding:

```
?filter=attributes%3Dname%3Dlike%3AJohn%20Doe    # John Doe
?filter=attributes%3Demail%3Dstartswith%3Atest%40 # test@
```

## Supported Operators

### Equality Operators

| Operator | Description | Example | SQL Equivalent |
|----------|-------------|---------|----------------|
| `eq` | Equal to | `filter=attributes=clientId=eq:122` | `Data @> '{"clientId":"122"}'` |
| `ne` | Not equal to | `filter=attributes=clientId=ne:122` | `NOT (Data @> '{"clientId":"122"}')` |

### Comparison Operators

| Operator | Description | Example | SQL Equivalent |
|----------|-------------|---------|----------------|
| `gt` | Greater than | `filter=attributes=testValue=gt:2` | `(Data ->> 'testValue')::numeric > 2` |
| `ge` | Greater than or equal | `filter=attributes=testValue=ge:3` | `(Data ->> 'testValue')::numeric >= 3` |
| `lt` | Less than | `filter=attributes=testValue=lt:4` | `(Data ->> 'testValue')::numeric < 4` |
| `le` | Less than or equal | `filter=attributes=testValue=le:3` | `(Data ->> 'testValue')::numeric <= 3` |
| `between` | Between two values | `filter=attributes=testValue=between:2,4` | `(Data ->> 'testValue')::numeric BETWEEN 2 AND 4` |

### String Operators

| Operator | Description | Example | SQL Equivalent |
|----------|-------------|---------|----------------|
| `like`/`match` | Contains substring | `filter=attributes=name=like:John` | `(Data ->> 'name') ILIKE '%John%'` |
| `startswith` | Starts with | `filter=attributes=email=startswith:test` | `(Data ->> 'email') ILIKE 'test%'` |
| `endswith` | Ends with | `filter=attributes=email=endswith:.com` | `(Data ->> 'email') ILIKE '%.com'` |

### Collection Operators

| Operator | Description | Example | SQL Equivalent |
|----------|-------------|---------|----------------|
| `in` | Value in list | `filter=attributes=clientId=in:122,177,83` | `(Data ->> 'clientId') IN ('122','177','83')` |
| `nin` | Value not in list | `filter=attributes=clientId=nin:122,177` | `(Data ->> 'clientId') NOT IN ('122','177')` |

## Data Type Support

The filtering system automatically handles different data types:

### String Values
```json
{"clientId": "177", "status": "active", "email": "user@example.com"}
```

**Examples:**
```
filter=attributes=clientId=eq:177
filter=attributes=status=ne:inactive
filter=attributes=email=endswith:@example.com
```

### Numeric Values
```json
{"testValue": 1, "amount": 99.99, "count": 42}
```

**Examples:**
```
filter=attributes=testValue=gt:2
filter=attributes=amount=between:50.00,150.00
filter=attributes=count=le:100
```

### Boolean Values
```json
{"isActive": true, "isVerified": false}
```

**Examples:**
```
filter=attributes=isActive=eq:true
filter=attributes=isVerified=ne:false
```

## Practical Examples

### Sample Data

Consider these workflow instances with the following `InstanceData.Data`:

```json
{"clientId": "177", "testValue": 1, "status": "pending", "amount": 99.50}
{"clientId": "110", "testValue": 2, "status": "active", "amount": 150.00}
{"clientId": "19", "testValue": 3, "status": "completed", "amount": 75.25}
{"clientId": "83", "testValue": 4, "status": "active", "amount": 200.00}
{"clientId": "122", "testValue": 4, "status": "pending", "amount": 125.75}
```

### Basic Filtering Examples

#### Find specific client
```
GET /api/v1.0/workflows/my-workflow/instances?filter=attributes=clientId=eq:122
```
**Result:** Returns instance with clientId "122"

#### Find all except specific client
```
GET /api/v1.0/workflows/my-workflow/instances?filter=attributes=clientId=ne:122
```
**Result:** Returns all instances except clientId "122"

#### Find instances with high test values
```
GET /api/v1.0/workflows/my-workflow/instances?filter=attributes=testValue=gt:2
```
**Result:** Returns instances with testValue 3 and 4

### Advanced Filtering Examples

#### Multiple conditions (AND logic)
```
GET /api/v1.0/workflows/my-workflow/instances?filter=attributes=status=eq:active&filter=attributes=testValue=ge:2
```
**Result:** Returns active instances with testValue >= 2

#### Range filtering
```
GET /api/v1.0/workflows/my-workflow/instances?filter=attributes=amount=between:100.00,200.00
```
**Result:** Returns instances with amount between 100.00 and 200.00

#### List filtering
```
GET /api/v1.0/workflows/my-workflow/instances?filter=attributes=clientId=in:110,122,83
```
**Result:** Returns instances for clients 110, 122, and 83

#### Text search
```
GET /api/v1.0/workflows/my-workflow/instances?filter=attributes=status=startswith:act
```
**Result:** Returns instances with status starting with "act" (e.g., "active")

#### Exclusion filtering
```
GET /api/v1.0/workflows/my-workflow/instances?filter=attributes=status=nin:completed,cancelled
```
**Result:** Returns instances that are NOT completed or cancelled

## Performance Considerations

### PostgreSQL Optimization

The filtering system uses PostgreSQL native JSONB operators for optimal performance:

- **JSONB Containment (`@>`)**: Used for equality checks, leverages GIN indexes
- **JSON Field Extraction (`->>`)**: Used for comparisons and text operations
- **Common Table Expressions (CTE)**: Optimizes complex queries with joins

### Recommended Indexes

For optimal performance, create these PostgreSQL indexes:

```sql
-- GIN index for JSONB containment operations
CREATE INDEX CONCURRENTLY idx_instances_data_gin 
ON "InstancesData" USING gin ("Data");

-- Specific field indexes for frequently queried fields
CREATE INDEX CONCURRENTLY idx_instances_data_client_id 
ON "InstancesData" (("Data" ->> 'clientId'));

CREATE INDEX CONCURRENTLY idx_instances_data_status 
ON "InstancesData" (("Data" ->> 'status'));
```

### Query Performance Tips

1. **Use specific operators**: `eq` with GIN indexes is faster than `like` operations
2. **Limit result sets**: Always use pagination for large datasets
3. **Index frequently filtered fields**: Create specific indexes for commonly queried JSON fields
4. **Avoid complex text searches**: Use dedicated search solutions for full-text search requirements

## API Integration

### Controller Usage

The filtering integrates seamlessly with the Instance Controller:

```csharp
[HttpGet("{domain}/workflows/{workflow}/instances")]
public async Task<IActionResult> GetInstanceListAsync(
    [FromRoute] string domain,
    [FromRoute] string workflow,
    [FromQuery] string[]? filter = null,
    [FromQuery] string[]? extension = null,
    [FromQuery] [Range(1, 1000)] int page = 1,
    [FromQuery] [Range(1, 100)] int pageSize = 10,
    [FromQuery] string? sort = null,
    CancellationToken cancellationToken = default)
```

### Response Format

```json
{
  "data": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "flow": "my-workflow",
      "flowVersion": "1.0.0",
      "domain": "my-domain",
      "key": "unique-key",
      "attributes": {
        "clientId": "122",
        "testValue": 4,
        "status": "pending"
      },
      "etag": "abc123def456"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 10,
    "totalCount": 1,
    "totalPages": 1
  }
}
```

## Error Handling

### Common Error Scenarios

#### Invalid Filter Format
```
HTTP 400 Bad Request
{
  "error": "Invalid filter format: clientId=invalid. Expected format: field=operator:value"
}
```

#### Unsupported Operator
```
HTTP 400 Bad Request
{
  "error": "Unsupported operator: xyz. Supported operators: eq, ne, gt, ge, lt, le, between, like, match, startswith, endswith, in, nin"
}
```

#### Invalid Data Type
```
HTTP 400 Bad Request
{
  "error": "Value 'abc' is not numeric for comparison operator 'gt'"
}
```

### Fallback Behavior

If PostgreSQL native filtering fails, the system automatically falls back to Entity Framework Core filtering:

```csharp
catch (Exception ex)
{
    // Fallback to EF Core filters
    Console.WriteLine($"PostgreSQL filter failed, falling back to EF Core filters: {ex.Message}");
    var filterSpec = new InstanceFilterSpecification(filters);
    return filterSpec.Apply(query);
}
```

## Best Practices

### Filter Design

1. **Use appropriate operators**: Choose the most specific operator for your use case
2. **Combine filters efficiently**: Order filters from most selective to least selective
3. **Validate input**: Always validate filter values on the client side
4. **Handle edge cases**: Account for null values and empty strings

### Security Considerations

1. **Input sanitization**: The system automatically sanitizes field names to prevent SQL injection
2. **Field validation**: Only alphanumeric characters, dots, and underscores are allowed in field names
3. **Parameter binding**: All values are properly parameterized in SQL queries

### Development Guidelines

1. **Test with real data**: Always test filters with production-like datasets
2. **Monitor performance**: Use query execution plans to optimize slow queries
3. **Document field types**: Maintain documentation of JSON schema for filtered fields
4. **Version compatibility**: Ensure filter compatibility across workflow versions

## Troubleshooting

### Common Issues

#### No Results Returned
- Verify field names match exactly (case-sensitive)
- Check data types (string vs numeric comparison)
- Ensure proper URL encoding of special characters

#### Slow Query Performance
- Add appropriate indexes for frequently filtered fields
- Use pagination to limit result sets
- Consider using simpler operators for large datasets

#### Filter Parsing Errors
- Validate filter syntax: `field=operator:value`
- Check for supported operators
- Ensure proper escaping of special characters

### Debug Tips

1. **Enable SQL logging**: Set logging level to debug to see generated SQL
2. **Use query profiling**: Analyze PostgreSQL query execution plans
3. **Test incrementally**: Start with simple filters and add complexity gradually

## Migration Guide

### From EF Core to PostgreSQL Native

If migrating from EF Core filtering to PostgreSQL native filtering:

1. **Audit existing filters**: Identify commonly used filter patterns
2. **Create indexes**: Add appropriate JSONB indexes before migration
3. **Test performance**: Compare query performance before and after migration
4. **Update documentation**: Update API documentation with new capabilities

### Version Compatibility

The filtering system is backward compatible and automatically handles:

- Legacy filter formats
- Different JSON schema versions
- Mixed data types in the same field across instances

## Conclusion

The instance filtering system provides a robust, high-performance solution for querying workflow instances based on their JSON attributes. By leveraging PostgreSQL native JSONB operations, it delivers excellent performance while maintaining flexibility and ease of use.

For additional questions or advanced use cases, refer to the source code in:
- `BBT.Workflow.Domain/QueryExtensions/PostgreSqlJsonFilterService.cs`
- `BBT.Workflow.Domain/QueryExtensions/FilterOperatorParser.cs`
- `BBT.Workflow.Infrastructure/Instances/EfCoreInstanceRepository.cs`
