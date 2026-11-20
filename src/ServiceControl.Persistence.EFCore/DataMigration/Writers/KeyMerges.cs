namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Finds the rows the insert will drop because another row, earlier in the batch or already in the table, has the same
/// key under the column's collation. A row the table already holds exactly is not a merge.
/// Returns each dropped row's source id with a line naming the key that was kept.
/// </summary>
static class KeyMerges
{
    public static async Task<IReadOnlyList<(string SourceId, string Detail)>> In<TEntity>(ServiceControlDbContext dbContext, IMigrationSqlDialect dialect, IReadOnlyList<(string SourceId, TEntity Row)> rows, CancellationToken cancellationToken = default) where TEntity : class
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity))!;
        var keyProperties = entityType.FindPrimaryKey()!.Properties;
        var queryColumn = keyProperties.FirstOrDefault(property => property.ClrType == typeof(string))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} has no text key column, so no collation can join two of its keys.");
        Func<object?, object?, bool>[] columnMatches = [.. keyProperties.Select(property => property.ClrType == typeof(string) ? TextMatch(dialect.KeyComparer(typeof(TEntity), property.Name)) : object.Equals)];

        object?[] KeyOf(TEntity entity) => [.. keyProperties.Select(property => property.GetGetter().GetClrValue(entity))];
        bool SameKey(object?[] first, object?[] second) => columnMatches.Select((matches, column) => matches(first[column], second[column])).All(same => same);

        var stored = new List<TEntity>();
        string[] queryValues = [.. rows.Select(row => (string)queryColumn.GetGetter().GetClrValue(row.Row)!).Distinct(StringComparer.Ordinal)];

        foreach (var chunk in queryValues.Chunk(dialect.RowsPerStatementFor<TEntity>(dbContext)))
        {
            stored.AddRange(await dbContext.Set<TEntity>().AsNoTracking().Where(entity => chunk.Contains(EF.Property<string>(entity, queryColumn.Name))).ToListAsync(cancellationToken));
        }

        // A stored key wins over every row of the batch, and an earlier row over a later one, as in the insert.
        var kept = stored.Select(entity => (Key: KeyOf(entity), Stored: true)).ToList();
        var merges = new List<(string SourceId, string Detail)>();

        foreach (var (sourceId, row) in rows)
        {
            var key = KeyOf(row);
            var match = kept.FindIndex(entry => SameKey(entry.Key, key));

            if (match < 0)
            {
                kept.Add((key, false));
            }
            else if (kept[match].Stored && kept[match].Key.SequenceEqual(key))
            {
                // Claimed by this row, so a later row of the batch with the same key is a merge.
                kept[match] = (key, false);
            }
            else
            {
                merges.Add((sourceId, $"{entityType.GetTableName()} treats {Describe(key)} and {Describe(kept[match].Key)} as one key, so it kept {Describe(kept[match].Key)} and dropped the row for {Describe(key)}"));
            }
        }

        return merges;
    }

    static Func<object?, object?, bool> TextMatch(IEqualityComparer<string> comparer) => (first, second) => comparer.Equals((string?)first, (string?)second);

    static string Describe(object?[] key) => string.Join(", ", key.Select(value => $"'{value}'"));
}
