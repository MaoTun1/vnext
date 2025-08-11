# Multi-Schema Support

## Overview

The BBT Workflow Engine provides comprehensive multi-schema support, enabling dynamic database schema creation and management for multi-flow scenarios. This architecture allows different workflows to operate in isolated database schemas while sharing the same application infrastructure, providing excellent separation of concerns and scalability.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                Application Layer                        │
│  ┌─────────────────┐  ┌─────────────────┐             │
│  │  Schema Context │  │ Schema Manager  │             │
│  │   Management    │  │                 │             │
│  │                 │  │ • Create Schema │             │
│  │ • Current Schema│  │ • Migrate Tables│             │
│  │ • Context Switch│  │ • Ensure Tables │             │
│  └─────────────────┘  └─────────────────┘             │
└─────────────────────────────────────────────────────────┘
                           │
┌─────────────────────────────────────────────────────────┐
│             Entity Framework Core                       │
│  ┌─────────────────┐  ┌─────────────────┐             │
│  │ Schema-Aware    │  │ Dynamic Model   │             │
│  │  DbContext      │  │   Caching       │             │
│  │                 │  │                 │             │
│  │ • Schema Name   │  │ • Per-Schema    │             │
│  │ • Model Config  │  │   Cache Keys    │             │
│  │ • Migration     │  │ • Cache Factory │             │
│  └─────────────────┘  └─────────────────┘             │
└─────────────────────────────────────────────────────────┘
                           │
┌─────────────────────────────────────────────────────────┐
│               Database Layer                            │
│  ┌─────────────────┐  ┌─────────────────┐             │
│  │ Schema: public  │  │ Schema: flow-a  │             │
│  │                 │  │                 │             │
│  │ • Default Schema│  │ • Flow A Tables │             │
│  │ • System Tables │  │ • Isolated Data │             │
│  │ • Migration Log │  │ • Flow A Indexes│             │
│  └─────────────────┘  └─────────────────┘             │
│                                                         │
│  ┌─────────────────┐  ┌─────────────────┐             │
│  │ Schema: flow-b  │  │ Schema: flow-c  │             │
│  │                 │  │                 │             │
│  │ • Flow B Tables │  │ • Flow C Tables │             │
│  │ • Isolated Data │  │ • Isolated Data │             │
│  │ • Flow B Indexes│  │ • Flow C Indexes│             │
│  └─────────────────┘  └─────────────────┘             │
└─────────────────────────────────────────────────────────┘
```

## Core Components

### 1. ICurrentSchema Interface

Manages the current database schema context:

```csharp
public interface ICurrentSchema
{
    string Name { get; }
    IDisposable Change(string name);
}
```

### 2. Current Schema Implementation

```csharp
public class CurrentSchema : ICurrentSchema
{
    private readonly ISchemaAccessor _schemaAccessor;

    public CurrentSchema(ISchemaAccessor schemaAccessor)
    {
        _schemaAccessor = schemaAccessor;
    }

    public string Name => _schemaAccessor.Current ?? "public";

    public IDisposable Change(string name)
    {
        return SetCurrent(name);
    }

    private IDisposable SetCurrent(string name)
    {
        var sanitizedName = SanitizeSchemaName(name);
        var previousSchema = _schemaAccessor.Current;
        
        _schemaAccessor.Current = sanitizedName;
        
        return new SchemaContextDisposable(() =>
        {
            _schemaAccessor.Current = previousSchema;
        });
    }

    private static string SanitizeSchemaName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "public";
            
        // Replace invalid characters with underscores
        return Regex.Replace(name, @"[^a-zA-Z0-9_]", "_").ToLowerInvariant();
    }
}
```

### 3. Schema Accessor with AsyncLocal Storage

```csharp
public class AsyncLocalSchemaAccessor : ISchemaAccessor
{
    public static AsyncLocalSchemaAccessor Instance { get; } = new();

    public string? Current
    {
        get => _currentScope.Value;
        set => _currentScope.Value = value;
    }

    private readonly AsyncLocal<string?> _currentScope;

    private AsyncLocalSchemaAccessor()
    {
        _currentScope = new AsyncLocal<string?>();
    }
}
```

## Schema Manager

### 1. ISchemaManager Interface

```csharp
public interface ISchemaManager
{
    Task EnsureSchemaAndTablesAsync(string schemaName, CancellationToken cancellationToken = default);
}
```

### 2. PostgreSQL Schema Manager Implementation

```csharp
public class PostgresSchemaManager : ISchemaManager
{
    private readonly IDbContextFactory<WorkflowDbContext> _dbContextFactory;

    public PostgresSchemaManager(IDbContextFactory<WorkflowDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task EnsureSchemaAndTablesAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        // Check for pending migrations and apply them if necessary
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Any())
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
```

## Schema-Aware DbContext

### 1. Workflow DbContext with Schema Support

```csharp
public class WorkflowDbContext : AetherDbContext<WorkflowDbContext>, IDbContextSchema
{
    private readonly ICurrentSchema _currentSchema;

    public WorkflowDbContext(
        DbContextOptions<WorkflowDbContext> options,
        ICurrentSchema currentSchema) : base(options)
    {
        _currentSchema = currentSchema;
    }

    public string? SchemaName => _currentSchema?.Name;

    // Entity sets
    public virtual DbSet<Instance> Instances { get; set; }
    public virtual DbSet<InstanceData> InstancesData { get; set; }
    public virtual DbSet<InstanceTransition> InstanceTransitions { get; set; }
    public virtual DbSet<InstanceTask> InstanceTasks { get; set; }
    public virtual DbSet<InstanceJob> InstanceJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Set the default schema for all entities
        builder.HasDefaultSchema(SchemaName);
        
        base.OnModelCreating(builder);

        // Configure workflow-specific entity mappings
        builder.ConfigureWorkflow(SchemaName);
    }
}
```

### 2. DbContext Factory for Schema Support

```csharp
public class WorkflowDbContextFactory : IDbContextFactory<WorkflowDbContext>
{
    private readonly ICurrentSchema _currentSchema;
    private readonly DbContextOptions<WorkflowDbContext> _options;

    public WorkflowDbContextFactory(
        ICurrentSchema currentSchema,
        DbContextOptions<WorkflowDbContext> options)
    {
        _currentSchema = currentSchema;
        _options = options;
    }

    public WorkflowDbContext CreateDbContext()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        
        var builder = new DbContextOptionsBuilder<WorkflowDbContext>(_options);

        builder.UseNpgsql(
            _options.Extensions.OfType<RelationalOptionsExtension>().First().ConnectionString,
            npgsqlOptions =>
            {
                // Set migration history table per schema
                npgsqlOptions.MigrationsHistoryTable("__Workflow_Migrations", _currentSchema.Name);
                
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            });

        // Enable schema-aware model caching
        builder.ReplaceService<IModelCacheKeyFactory, DynamicSchemaModelCacheKeyFactory>();
        builder.ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>();
        
        return new WorkflowDbContext(builder.Options, _currentSchema);
    }
}
```

## Dynamic Model Caching

### 1. Schema-Aware Model Cache Key Factory

The default Entity Framework Core model caching doesn't account for different schemas. This factory ensures each schema gets its own model cache:

```csharp
public class DynamicSchemaModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => new CoreModelCacheKey(context, designTime);
}

class CoreModelCacheKey : ModelCacheKey
{
    readonly string? _schema = (context as WorkflowDbContext)?.SchemaName ?? "public";
    readonly bool _designTime = designTime;
    
    protected override bool Equals(ModelCacheKey other)
    {
        return base.Equals(other)
               && other is CoreModelCacheKey otherKey
               && otherKey._schema == _schema
               && otherKey._designTime == _designTime;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), _schema);
    }
}
```

### 2. Schema-Aware Migration Assembly

```csharp
public class DbSchemaAwareMigrationAssembly : MigrationsAssembly
{
    private readonly DbContext _context;

    public DbSchemaAwareMigrationAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions options,
        IMigrationsIdGenerator idGenerator,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
        : base(currentContext, options, idGenerator, logger)
    {
        _context = currentContext.Context;
    }

    public override Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        if (activeProvider == null)
            throw new ArgumentNullException(nameof(activeProvider));
            
        var hasCtorWithSchema = migrationClass
            .GetConstructor(new[] { typeof(IDbContextSchema) }) != null;
            
        if (hasCtorWithSchema && _context is IDbContextSchema schema)
        {
            var instance = (Migration?)Activator.CreateInstance(
                migrationClass.AsType(), schema);
                
            if (instance != null)
            {
                instance.ActiveProvider = activeProvider;
                return instance;
            }
        }
        
        return base.CreateMigration(migrationClass, activeProvider);
    }
}
```

## Schema Context Usage Patterns

### 1. Basic Schema Switching

```csharp
public class SomeService
{
    private readonly ICurrentSchema _currentSchema;
    private readonly IInstanceRepository _instanceRepository;

    public async Task ProcessWorkflowAsync(string flowName, string instanceKey)
    {
        // Switch to the workflow's schema
        using (_currentSchema.Change(flowName))
        {
            // All database operations now use the specified schema
            var instance = await _instanceRepository.FindByKeyAsync(instanceKey);
            
            if (instance != null)
            {
                // Process the instance
                await ProcessInstanceAsync(instance);
            }
        }
        // Schema context is automatically restored when disposed
    }
}
```

### 2. Admin Service with Schema Management

```csharp
public class AdminAppService : IAdminAppService
{
    public async Task PublishAsync(PublishInput input, CancellationToken cancellationToken = default)
    {
        // Validate runtime schema configuration
        _runtimeInfoProvider.Check(input.Domain);
        
        // Switch to the workflow's schema
        using (_currentSchema.Change(input.Flow))
        {
            // Ensure schema and tables exist
            await _schemaManager.EnsureSchemaAndTablesAsync(_currentSchema.Name, cancellationToken);
            
            // Find or create workflow instance
            var instance = await _instanceRepository.FindByKeyAsync(input.Key, cancellationToken)
                           ?? Instance.Create(GuidGenerator.Create(), input.Flow, input.Key);

            // Process and save the workflow definition
            await ProcessWorkflowDefinitionAsync(instance, input, cancellationToken);
        }
    }
}
```

### 3. Runtime Service with Multi-Schema Loading

```csharp
public class RuntimeService : IRuntimeService
{
    public async Task<IEnumerable<T?>> GetAsync<T>(string schema, CancellationToken cancellationToken = default)
        where T : class, IDomainEntity, IReferenceSetter
    {
        var schemaInfo = _runtimeOptions.Value.Schemas[schema];

        using (_currentSchema.Change(schemaInfo.Schema))
        {
            // Ensure schema exists
            await _schemaManager.EnsureSchemaAndTablesAsync(_currentSchema.Name, cancellationToken);

            // Load components from the schema
            var results = await _instanceRepository.GetActiveDataListAsync(cancellationToken);
            
            var components = results
                .Select(item => DeserializeComponent<T>(item, schemaInfo))
                .Where(component => component != null)
                .ToList();
                
            return components;
        }
    }
}
```

## Migration Management

### 1. Schema-Aware Migrations

```csharp
public partial class Initial : Migration
{
    private readonly IDbContextSchema _schema;  
    
    public Initial(IDbContextSchema schema)
    {
        _schema = schema;
    }
    
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Ensure schema exists
        migrationBuilder.EnsureSchema(name: _schema.SchemaName);

        // Create tables in the specified schema
        migrationBuilder.CreateTable(
            name: "Instances",
            schema: _schema.SchemaName,
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Flow = table.Column<string>(type: "text", nullable: false),
                CurrentState = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Status = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Instances", x => x.Id);
            });

        // Create indexes in the schema
        migrationBuilder.CreateIndex(
            name: "IX_Instances_Key",
            schema: _schema.SchemaName,
            table: "Instances",
            column: "Key",
            unique: true,
            filter: "[Key] IS NOT NULL");
    }
}
```

### 2. Design-Time DbContext Factory

```csharp
public sealed class WorkflowDbContextDesignFactory : IDesignTimeDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var optionsBuilder = new DbContextOptionsBuilder<WorkflowDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=Aether_WorkflowDb;Username=postgres;Password=postgres;",
            npgsqlOptions => 
            { 
                npgsqlOptions.MigrationsHistoryTable("__Workflow_Migrations"); 
            });

        return new WorkflowDbContext(
            optionsBuilder.Options,
            new CurrentSchema(AsyncLocalSchemaAccessor.Instance)
        );
    }
}
```

## Job Store with Schema Support

### 1. Schema-Aware Job Storage

```csharp
public sealed class EfCoreJobStore : IJobStore
{
    private readonly ICurrentSchema _currentSchema;
    private readonly IInstanceJobRepository _jobRepository;

    public async Task SaveAsync<T>(string jobId, BackgroundJobInfo<T> jobInfo, CancellationToken cancellationToken = default) where T : class
    {
        // Extract schema from job ID or use default logic
        var schemaName = ExtractSchemaFromJobId(jobId);
        
        using (_currentSchema.Change(schemaName))
        {
            var existingJob = await _jobRepository.FindByJobIdAsync(jobId, cancellationToken);
            
            if (existingJob != null)
            {
                // Update existing job
                existingJob.UpdatePayload(JsonSerializer.Serialize(jobInfo));
                await _jobRepository.UpdateAsync(existingJob, cancellationToken: cancellationToken);
            }
            else
            {
                // Create new job in the current schema
                var instanceJob = CreateInstanceJob(jobId, jobInfo);
                await _jobRepository.InsertAsync(instanceJob, cancellationToken: cancellationToken);
            }
        }
    }

    private string ExtractSchemaFromJobId(string jobId)
    {
        // Extract schema from job ID pattern: "timeout-{flowName}-{instanceId}"
        var parts = jobId.Split('-');
        return parts.Length > 1 ? parts[1] : "public";
    }
}
```

## Configuration and Setup

### 1. Dependency Injection Registration

```csharp
public static IServiceCollection AddInfrastructureModule(this IServiceCollection services)
{
    // Schema management
    services.AddSingleton<ISchemaAccessor>(AsyncLocalSchemaAccessor.Instance);
    services.AddScoped<ICurrentSchema, CurrentSchema>();
    services.AddScoped<ISchemaManager, PostgresSchemaManager>();
    
    // DbContext with schema support
    services.AddScoped<IDbContextFactory<WorkflowDbContext>, WorkflowDbContextFactory>();
    
    // Repositories (schema-aware)
    services.AddScoped<IInstanceRepository, EfCoreInstanceRepository>();
    services.AddScoped<IInstanceJobRepository, EfCoreInstanceJobRepository>();
    
    return services;
}
```

### 2. Runtime Schema Configuration

```csharp
public class RuntimeOptions
{
    public RuntimeSysSchemaDictionary Schemas { get; } = new();
}

// Configure runtime schemas
services.Configure<RuntimeOptions>(options =>
{
    options.Schemas.Add(RuntimeSysSchemaInfo.Flows, new RuntimeSysSchemaInfo(
        name: "sys-flows",
        schema: "loan-approval",
        type: typeof(Workflow)
    ));
    
    options.Schemas.Add(RuntimeSysSchemaInfo.Tasks, new RuntimeSysSchemaInfo(
        name: "sys-tasks", 
        schema: "loan-approval",
        type: typeof(WorkflowTask)
    ));
});
```

## Best Practices

### 1. Schema Naming Conventions

```csharp
public static class SchemaNameConventions
{
    public static string SanitizeSchemaName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "public";
            
        // Convert to lowercase and replace invalid characters
        var sanitized = input.ToLowerInvariant();
        sanitized = Regex.Replace(sanitized, @"[^a-z0-9_]", "_");
        
        // Ensure it starts with a letter or underscore
        if (!Regex.IsMatch(sanitized, @"^[a-z_]"))
            sanitized = "_" + sanitized;
            
        // Limit length
        if (sanitized.Length > 63) // PostgreSQL identifier limit
            sanitized = sanitized.Substring(0, 63);
            
        return sanitized;
    }
}
```

### 2. Error Handling for Schema Operations

```csharp
public class SchemaAwareService
{
    public async Task<T> ExecuteInSchemaAsync<T>(string schemaName, Func<Task<T>> operation)
    {
        try
        {
            using (_currentSchema.Change(schemaName))
            {
                // Ensure schema exists before operation
                await _schemaManager.EnsureSchemaAndTablesAsync(_currentSchema.Name);
                
                return await operation();
            }
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "3F000") // schema does not exist
        {
            _logger.LogError(ex, "Schema {SchemaName} does not exist", schemaName);
            throw new SchemaNotFoundException(schemaName, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing operation in schema {SchemaName}", schemaName);
            throw;
        }
    }
}
```

### 3. Performance Considerations

```csharp
public class SchemaCache
{
    private readonly ConcurrentDictionary<string, bool> _schemaExistsCache = new();
    
    public async Task<bool> SchemaExistsAsync(string schemaName)
    {
        return _schemaExistsCache.GetOrAdd(schemaName, async name =>
        {
            // Check if schema exists in database
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = new NpgsqlCommand(
                "SELECT 1 FROM information_schema.schemata WHERE schema_name = @schema", 
                connection);
            command.Parameters.AddWithValue("@schema", name);
            
            var result = await command.ExecuteScalarAsync();
            return result != null;
        });
    }
    
    public void InvalidateCache(string schemaName)
    {
        _schemaExistsCache.TryRemove(schemaName, out _);
    }
}
```

## Usage Examples

### 1. Multi-Flow Workflow Processing

```csharp
public class WorkflowProcessor
{
    public async Task ProcessMultipleFlowsAsync()
    {
        var flows = new[] { "loan-approval", "customer-onboarding", "document-processing" };
        
        foreach (var flow in flows)
        {
            using (_currentSchema.Change(flow))
            {
                _logger.LogInformation("Processing workflow in schema: {Schema}", _currentSchema.Name);
                
                // All operations in this block use the current flow's schema
                var activeInstances = await _instanceRepository.GetActiveInstancesAsync();
                
                foreach (var instance in activeInstances)
                {
                    await ProcessInstanceAsync(instance);
                }
            }
        }
    }
}
```

### 2. Schema Migration Management

```csharp
public class SchemaMigrationService
{
    public async Task MigrateAllSchemasAsync()
    {
        var schemas = await GetAllWorkflowSchemasAsync();
        
        foreach (var schema in schemas)
        {
            try
            {
                using (_currentSchema.Change(schema))
                {
                    _logger.LogInformation("Migrating schema: {Schema}", schema);
                    await _schemaManager.EnsureSchemaAndTablesAsync(schema);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate schema: {Schema}", schema);
                // Continue with next schema
            }
        }
    }
}
```

### 3. Cross-Schema Data Analysis

```csharp
public class CrossSchemaAnalytics
{
    public async Task<AnalyticsReport> GenerateReportAsync()
    {
        var report = new AnalyticsReport();
        var schemas = await GetWorkflowSchemasAsync();
        
        foreach (var schema in schemas)
        {
            using (_currentSchema.Change(schema))
            {
                var stats = await _analyticsRepository.GetSchemaStatsAsync();
                report.SchemaStats[schema] = stats;
            }
        }
        
        return report;
    }
}
```

The multi-schema support provides excellent isolation and scalability for workflow processing, enabling the same application to handle multiple distinct workflow types while maintaining data separation and performance optimization. 