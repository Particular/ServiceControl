namespace ServiceControl.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;

/// <summary>
/// The migration's SQL Server statements. Two keys that differ only in case can be one row here, because a
/// column's collation decides what counts as equal, so the statements compare keys under the collation the
/// schema gives them and the batch is de-duplicated the same way before it is offered to the table.
/// </summary>
class SqlServerMigrationSqlDialect : SqlServerDialect, IMigrationSqlDialect
{
    const int IgnoreCaseStyle = 1;

    OpenedKeys? OpenedState { get; set; }

    public async Task Open(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var sql = dbContext.GetService<ISqlGenerationHelper>();
        var collations = new Dictionary<(string Table, string Column), string>();
        var comparers = new Dictionary<(Type EntityType, string Property), IEqualityComparer<string>>();

        foreach (var entityType in dbContext.Model.GetEntityTypes())
        {
            if (entityType.GetTableName() is not { } tableName || entityType.FindPrimaryKey() is not { } key)
            {
                continue;
            }

            var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            var table = sql.DelimitIdentifier(tableName, entityType.GetSchema());

            foreach (var property in key.Properties.Where(property => property.ClrType == typeof(string)))
            {
                // A key property with no column of its own is refused by name when MigrationInsert builds the statement.
                if (property.GetColumnName(storeObject) is not { } column)
                {
                    continue;
                }

                var collation = await dbContext.Database
                    .SqlQuery<KeyColumnCollation>($"""
                        SELECT c.collation_name AS [Name],
                               CONVERT(int, COLLATIONPROPERTY(c.collation_name, 'ComparisonStyle')) AS [ComparisonStyle]
                        FROM sys.columns AS c
                        WHERE c.object_id = OBJECT_ID({table}) AND c.name = {column} AND c.collation_name IS NOT NULL
                        """)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException($"The migration target found no collation for {tableName}.{column}, so the SQL Server schema is missing or not current. Run ServiceControl with --setup against this database first.");

                collations[(table, sql.DelimitIdentifier(column))] = collation.Name;
                comparers[(entityType.ClrType, property.Name)] = (collation.ComparisonStyle & IgnoreCaseStyle) == 0 ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            }
        }

        OpenedState = new OpenedKeys(collations, comparers);
    }

    public IEqualityComparer<string> KeyComparer(Type entityType, string propertyName) =>
        Keys.Comparers.TryGetValue((entityType, propertyName), out var comparer)
            ? comparer
            : throw new ArgumentException($"{entityType.Name}.{propertyName} is not a string primary-key column, so it has no collation to compare under.");

    public int RowsPerStatement(int parametersPerRow) => MaxRowsPerStatement(parametersPerRow);

    public async Task<IReadOnlyList<TEntity>> InsertMissing<TEntity>(ServiceControlDbContext dbContext, IReadOnlyList<TEntity> rows, CancellationToken cancellationToken = default) where TEntity : class
    {
        var insert = MigrationInsert<TEntity>.For(dbContext);
        var columns = string.Join(", ", insert.Columns);
        // The collation is an identifier sys.columns returned, and COLLATE takes no parameter.
        var partitionBy = string.Join(", ", insert.KeyColumns.Select(column =>
            Keys.Collations.TryGetValue((insert.Table, column), out var collation) ? $"{column} COLLATE {collation}" : column));
        var inserted = new List<TEntity>(rows.Count);

        foreach (var chunk in rows.Chunk(RowsPerStatement(insert.Columns.Count)))
        {
            // HOLDLOCK, or two writers can both find a key missing and both insert it.
            inserted.AddRange(await insert.Execute(
                dbContext,
                $"""
                 MERGE {insert.Table} WITH (HOLDLOCK) AS t
                 USING (
                     SELECT {columns}
                     FROM (
                         SELECT {columns}, ROW_NUMBER() OVER (PARTITION BY {partitionBy} ORDER BY [MigrationOrdinal]) AS [MigrationDuplicate]
                         FROM (VALUES
                 {ParameterRowsWithOrdinal(chunk.Length, insert.Columns.Count)}
                         ) AS v ({columns}, [MigrationOrdinal])
                     ) AS deduplicated
                     WHERE [MigrationDuplicate] = 1
                 ) AS s ({columns})
                 ON {string.Join(" AND ", insert.KeyColumns.Select(column => $"t.{column} = s.{column}"))}
                 WHEN NOT MATCHED THEN INSERT ({columns}) VALUES ({string.Join(", ", insert.Columns.Select(column => $"s.{column}"))})
                 OUTPUT {string.Join(", ", insert.KeyColumns.Select(column => $"inserted.{column}"))};
                 """,
                chunk,
                cancellationToken));
        }

        return inserted;
    }

    OpenedKeys Keys => OpenedState ?? throw new InvalidOperationException($"The SQL Server migration dialect is not open. Call {nameof(Open)} first.");

    // SqlServerDialect.ParameterRows numbers the parameters the same way, but has no room for the position each row arrived in, which the de-duplication orders by.
    static string ParameterRowsWithOrdinal(int rowCount, int columnCount) =>
        string.Join(",\n", Enumerable.Range(0, rowCount).Select(row =>
            $"({string.Join(", ", Enumerable.Range(0, columnCount).Select(column => $"@p{(row * columnCount) + column}"))}, {row})"));

    sealed record OpenedKeys(
        IReadOnlyDictionary<(string Table, string Column), string> Collations,
        IReadOnlyDictionary<(Type EntityType, string Property), IEqualityComparer<string>> Comparers);

    sealed record KeyColumnCollation(string Name, int ComparisonStyle);
}
