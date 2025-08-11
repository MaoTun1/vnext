using System.Reflection;
using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;

namespace BBT.Workflow.Schemas;

/// <summary>
/// Provides a custom migrations assembly that supports dynamic database schema creation and migration management.
/// This class extends the default Entity Framework Core migration assembly to enable schema-aware migration execution,
/// allowing different database schemas to be created dynamically based on the current database context.
/// </summary>
/// <remarks>
/// This implementation is specifically designed for multi-tenant or multi-schema scenarios where database migrations
/// need to be executed against different schemas at runtime. The class integrates with the workflow's schema management
/// infrastructure to ensure migrations are applied to the correct database schema context.
/// </remarks>
/// <param name="currentContext">The current database context service that provides access to the active DbContext instance.</param>
/// <param name="options">The database context options that configure the DbContext behavior and connection settings.</param>
/// <param name="idGenerator">The migration ID generator service used for creating unique migration identifiers.</param>
/// <param name="logger">The diagnostic logger for capturing migration-related events and debugging information.</param>
public class DbSchemaAwareMigrationAssembly(
    ICurrentDbContext currentContext,
    IDbContextOptions options,
    IMigrationsIdGenerator idGenerator,
    IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
#pragma warning disable EF1001
    : MigrationsAssembly(currentContext, options, idGenerator, logger)
#pragma warning restore EF1001
{
    /// <summary>
    /// The database context instance extracted from the current context service.
    /// This field holds the reference to the active DbContext for schema-aware operations.
    /// </summary>
    private readonly DbContext _context = currentContext.Context;

    /// <summary>
    /// Creates a migration instance with schema-aware capabilities, enabling dynamic schema support during migration execution.
    /// This method checks if the migration class supports schema-aware construction and creates instances accordingly.
    /// </summary>
    /// <param name="migrationClass">The Type information for the migration class that needs to be instantiated.</param>
    /// <param name="activeProvider">The name of the active database provider (e.g., "PostgreSQL", "SqlServer") that will execute the migration.</param>
    /// <returns>
    /// A <see cref="Migration"/> instance configured for the specified database provider. If the migration class supports
    /// schema-aware construction and the context implements <see cref="IDbContextSchema"/>, a schema-aware migration instance
    /// is created. Otherwise, falls back to the default migration creation behavior.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="activeProvider"/> is null, as the provider information is required for migration execution.
    /// </exception>
    /// <remarks>
    /// The method performs the following operations:
    /// 1. Validates that the activeProvider parameter is not null
    /// 2. Checks if the migration class has a constructor that accepts an <see cref="IDbContextSchema"/> parameter
    /// 3. If schema-aware construction is supported and the context implements <see cref="IDbContextSchema"/>, 
    ///    creates a migration instance with schema information
    /// 4. Sets the ActiveProvider property on the created migration instance
    /// 5. Falls back to base class behavior if schema-aware construction is not available
    /// 
    /// This approach enables migrations to be executed against specific database schemas while maintaining
    /// compatibility with standard Entity Framework Core migration patterns.
    /// </remarks>
    public override Migration CreateMigration(
        TypeInfo migrationClass, 
        string activeProvider)
    {
        if (activeProvider == null)
            throw new ArgumentNullException(nameof(activeProvider));
            
        var hasCtorWithSchema = migrationClass
            .GetConstructor(new[] { typeof(IDbContextSchema) }) != null;
            
        if (hasCtorWithSchema && _context is IDbContextSchema schema)
        {
            var instance = (Migration?)
                Activator.CreateInstance(migrationClass.AsType(), 
                    schema);
            if (instance != null)
            {
                instance.ActiveProvider = activeProvider;
                return instance;
            }
        }
        
        return base.CreateMigration(migrationClass, activeProvider);
    }
}