using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace BBT.Workflow.Schemas;

public sealed class MultiSchemaNpgsqlMigrationsSqlGenerator(
    MigrationsSqlGeneratorDependencies dependencies,
    INpgsqlSingletonOptions npgsqlSingletonOptions)
    : NpgsqlMigrationsSqlGenerator(dependencies, npgsqlSingletonOptions)
{
#pragma warning disable EF1001 // Internal EF Core / Npgsql API usage
#pragma warning restore EF1001
    
    protected override void Generate(
        MigrationOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        StripDefaultPublicSchema(operation);
        base.Generate(operation, model, builder);
    }

    private static void StripDefaultPublicSchema(MigrationOperation operation)
    {
        switch (operation)
        {
            // TABLES
            case CreateTableOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                break;

            case DropTableOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                break;

            case RenameTableOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                op.NewSchema = NormalizeSchema(op.NewSchema);
                break;

            // COLUMNS
            case AddColumnOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                break;

            case AlterColumnOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                break;

            case DropColumnOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                break;

            case RenameColumnOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                break;

            // PRIMARY KEY
            case AddPrimaryKeyOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                break;

            case DropPrimaryKeyOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                break;

            // FOREIGN KEY
            case AddForeignKeyOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                op.PrincipalSchema = NormalizeSchema(op.PrincipalSchema);
                if (string.IsNullOrWhiteSpace(op.PrincipalSchema) && !string.IsNullOrWhiteSpace(op.Schema))
                {
                    op.PrincipalSchema = op.Schema;
                }
                break;

            case DropForeignKeyOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                break;

            // INDEX
            case CreateIndexOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                break;

            case DropIndexOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                break;

            case RenameIndexOperation op:
                op.Schema = NormalizeSchema(op.Schema);
                break;
            
            case EnsureSchemaOperation _:
            case DropSchemaOperation _:
                break;
        }
    }

    private static string? NormalizeSchema(string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
            return schema;
        
        return schema.Equals("public", StringComparison.OrdinalIgnoreCase)
            ? null
            : schema;
    }
}