namespace ServiceControl.Persistence.EFCore.PostgreSql;

using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;

/// <summary>
/// The migration's PostgreSQL statements. An insert that does nothing on a conflicting key is all it takes to
/// add only what is absent, and the keys it inserted come back from the same statement.
/// </summary>
class PostgreSqlMigrationSqlDialect : PostgreSqlDialect, IMigrationSqlDialect
{
    // Deterministic collations compare keys byte for byte, so there is no schema fact to read and none to name in the statement.
    public Task Open(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IEqualityComparer<string> KeyComparer(Type entityType, string propertyName) => StringComparer.Ordinal;

    // A fixed chunk, because PostgreSQL's parameter limit is far away and the row width does not bring it closer.
    public int RowsPerStatement(int parametersPerRow) => MaxRowsPerStatement;

    public async Task<IReadOnlyList<TEntity>> InsertMissing<TEntity>(ServiceControlDbContext dbContext, IReadOnlyList<TEntity> rows, CancellationToken cancellationToken = default) where TEntity : class
    {
        var insert = MigrationInsert<TEntity>.For(dbContext);
        var keyColumns = string.Join(", ", insert.KeyColumns);
        var inserted = new List<TEntity>(rows.Count);

        foreach (var chunk in rows.Chunk(RowsPerStatement(insert.Columns.Count)))
        {
            inserted.AddRange(await insert.Execute(
                dbContext,
                $"""
                 INSERT INTO {insert.Table} ({string.Join(", ", insert.Columns)})
                 VALUES
                 {ParameterRows(chunk.Length, insert.Columns.Count)}
                 ON CONFLICT ({keyColumns}) DO NOTHING
                 RETURNING {keyColumns}
                 """,
                chunk,
                cancellationToken));
        }

        return inserted;
    }
}
