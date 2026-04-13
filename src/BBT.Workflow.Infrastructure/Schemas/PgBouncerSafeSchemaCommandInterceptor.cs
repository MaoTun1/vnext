using System.Data.Common;
using BBT.Aether.MultiSchema;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace BBT.Workflow.Schemas;

/// <summary>
/// EF Core command interceptor that prepends <c>SET search_path</c> to every SQL command
/// before execution, making schema resolution safe under PgBouncer transaction-mode pooling.
///
/// Unlike <c>NpgsqlSchemaConnectionInterceptor</c> which sets <c>search_path</c> once on
/// connection open (session-level), this interceptor injects the directive inline into each
/// command so the correct schema is guaranteed regardless of whether the underlying backend
/// connection was recycled by PgBouncer between transactions.
///
/// The <c>SET search_path</c> statement is prepended to the existing SQL text and sent as a
/// single wire message — no extra round-trip is incurred.
/// </summary>
public sealed class PgBouncerSafeSchemaCommandInterceptor : DbCommandInterceptor
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="PgBouncerSafeSchemaCommandInterceptor"/>.
    /// </summary>
    public PgBouncerSafeSchemaCommandInterceptor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        PrependSetSearchPath(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        PrependSetSearchPath(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        PrependSetSearchPath(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void PrependSetSearchPath(DbCommand command)
    {
        if (command.CommandText.StartsWith("SET search_path", StringComparison.OrdinalIgnoreCase))
            return;

        using var scope = _scopeFactory.CreateScope();
        var currentSchema = scope.ServiceProvider.GetRequiredService<ICurrentSchema>();

        if (string.IsNullOrWhiteSpace(currentSchema.Name))
            return;

        command.CommandText = $"SET search_path = \"{currentSchema.Name}\";\n{command.CommandText}";
    }
}
