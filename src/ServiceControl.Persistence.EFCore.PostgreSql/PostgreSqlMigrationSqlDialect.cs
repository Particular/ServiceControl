namespace ServiceControl.Persistence.EFCore.PostgreSql;

using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
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

    // A varchar(n) column counts characters, which are code points, so a pair of UTF-16 surrogates is one.
    public int TextLength(string value) => value.EnumerateRunes().Count();

    // PostgreSQL text types cannot hold U+0000 in any encoding, and refuse the statement rather than drop it.
    public char? RejectedCharacterIn(string value) => value.Contains('\0') ? '\0' : null;

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
                configureParameter: null,
                cancellationToken));
        }

        return inserted;
    }

    public async Task<int> AddLicensingEndpointThroughput(ServiceControlDbContext dbContext, IReadOnlyList<LicensingEndpointThroughputEntity> rows, CancellationToken cancellationToken = default)
    {
        var insert = MigrationInsert<LicensingEndpointThroughputEntity>.For(dbContext);
        var columns = string.Join(", ", insert.Columns);
        var keyColumns = string.Join(", ", insert.KeyColumns);
        var counts = insert.Columns.Except(insert.KeyColumns).ToList();
        var written = 0;

        foreach (var chunk in rows.Chunk(RowsPerStatement(insert.Columns.Count)))
        {
            // Grouped first, because ON CONFLICT refuses a statement that reaches one row twice.
            written += (await insert.Execute(
                dbContext,
                $"""
                 INSERT INTO {insert.Table} ({columns})
                 SELECT {string.Join(", ", insert.Columns.Select(column => counts.Contains(column) ? $"SUM({column})" : column))}
                 FROM (VALUES
                 {ParameterRows(chunk.Length, insert.Columns.Count)}
                 ) AS v ({columns})
                 GROUP BY {keyColumns}
                 ON CONFLICT ({keyColumns})
                 DO UPDATE SET {string.Join(", ", counts.Select(column => $"{column} = {insert.Table}.{column} + excluded.{column}"))}
                 RETURNING {keyColumns}
                 """,
                chunk,
                configureParameter: null,
                cancellationToken)).Count;
        }

        return written;
    }
}
