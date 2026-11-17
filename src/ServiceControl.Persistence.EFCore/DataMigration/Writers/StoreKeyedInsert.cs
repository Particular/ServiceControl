namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes rows whose primary key the database generates.
/// The model-driven insert refuses such rows, so this class adds them through EF in the caller's context, and the database assigns each key.
/// It also holds its own copy of the text check, because the base writer's check needs that insert.
/// </summary>
static class StoreKeyedInsert
{
    // EF splits its own statements under each provider's limits, so this sizes only how many rows one checkpoint covers.
    public const int BatchSize = 500;

    /// <summary>
    /// Returns every text column of <typeparamref name="TEntity" />, each with its name as "table.column" for a skip's detail. Read it once per batch and pass it to <see cref="UnstorableValueSkip{TEntity}" />.
    /// </summary>
    public static (IProperty Property, string TableAndColumn)[] TextColumns<TEntity>(ServiceControlDbContext dbContext) where TEntity : class
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity))!;

        return [.. entityType.GetProperties().Where(property => property.ClrType == typeof(string)).Select(property => (property, $"{entityType.GetTableName()}.{property.GetColumnName()}"))];
    }

    /// <summary>
    /// Returns a skip when <paramref name="entity" />, the row mapped from the source document in <paramref name="row" />, holds a text value the database would refuse, or null when every value can be stored.
    /// The reason is ValueTooLong for a value longer than its column, or MalformedValue for a value holding a character the provider cannot store. The skip carries the source id of <paramref name="row" />, and its detail names the table, the column and the document.
    /// </summary>
    public static (string SourceId, MigrationSkipReason Reason, string Detail)? UnstorableValueSkip<TEntity>((IProperty Property, string TableAndColumn)[] textColumns, IMigrationSqlDialect dialect, MigrationRow row, TEntity entity) where TEntity : class
    {
        foreach (var (property, tableAndColumn) in textColumns)
        {
            if (property.GetGetter().GetClrValue(entity) is string { Length: var units } value && property.GetMaxLength() is { } maxLength && units > maxLength && dialect.TextLength(value) is var length && length > maxLength)
            {
                return (row.SourceId, MigrationSkipReason.ValueTooLong, $"{tableAndColumn} takes at most {maxLength} characters and the value holds {length}, on document '{row.SourceId}'");
            }
        }

        foreach (var (property, tableAndColumn) in textColumns)
        {
            if (property.GetGetter().GetClrValue(entity) is string value && dialect.RejectedCharacterIn(value) is { } rejected)
            {
                return (row.SourceId, MigrationSkipReason.MalformedValue, $"{tableAndColumn} cannot hold the character U+{(int)rejected:X4}, which the value contains, on document '{row.SourceId}'");
            }
        }

        return null;
    }

    /// <summary>
    /// Builds a new entity for each source row with <paramref name="map" />, adds them all to the caller's context in the order given, and saves inside the transaction the target already holds.
    /// Returns how many rows it added, which is all of them, because the save writes every row or throws. Call it inside the target's insert, so each attempt of the execution strategy builds fresh entities.
    /// </summary>
    public static async Task<int> AddAll<TSource, TEntity>(ServiceControlDbContext dbContext, IReadOnlyList<TSource> sources, Func<TSource, TEntity> map, CancellationToken cancellationToken = default) where TEntity : class
    {
        // Mapped on every call, because a failed save can leave generated ids on the entities it tried.
        dbContext.Set<TEntity>().AddRange(sources.Select(map));
        await dbContext.SaveChangesAsync(cancellationToken);

        return sources.Count;
    }
}
