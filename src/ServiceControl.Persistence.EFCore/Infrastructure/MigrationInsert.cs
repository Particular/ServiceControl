namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using ServiceControl.Persistence.EFCore.DbContexts;

/// <summary>
/// The table, columns and primary key <typeparamref name="TEntity"/> maps to, and the run of one provider's insert statement over a chunk of those rows.
/// </summary>
public sealed class MigrationInsert<TEntity> where TEntity : class
{
    static readonly ExactKey KeyEquality = new();

    readonly IReadOnlyList<IProperty> properties;
    readonly IReadOnlyList<IProperty> keyProperties;

    MigrationInsert(string table, IReadOnlyList<IProperty> properties, IReadOnlyList<IProperty> keyProperties, Func<IProperty, string> columnName)
    {
        Table = table;
        this.properties = properties;
        this.keyProperties = keyProperties;
        Columns = [.. properties.Select(columnName)];
        KeyColumns = [.. keyProperties.Select(columnName)];
    }

    /// <summary>The table name, quoted for this provider and carrying its schema.</summary>
    public string Table { get; }

    /// <summary>Every column the insert writes, quoted, in the order the parameters of one row follow.</summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>The primary key columns, quoted, in the order the statement has to return them.</summary>
    public IReadOnlyList<string> KeyColumns { get; }

    /// <summary>
    /// Reads the table, columns and key out of the EF Core model for this entity.
    /// </summary>
    /// <exception cref="InvalidOperationException">The entity is not mapped, has no primary key, or has a column this insert cannot write, such as one the database generates. Rows like that go through EF in the caller's context instead.</exception>
    public static MigrationInsert<TEntity> For(ServiceControlDbContext dbContext)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not an entity in the EF Core model, so the migration cannot tell which table it lands in.");
        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped to a table.");
        var key = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} has no primary key, so there is nothing for a row to be absent by.");
        IProperty[] properties = [.. entityType.GetProperties()];

        if (properties.Any(property => property.ValueGenerated != ValueGenerated.Never)
            || entityType.GetComplexProperties().Any()
            || entityType.GetNavigations().Any(navigation => navigation.TargetEntityType.IsOwned()))
        {
            throw new InvalidOperationException($"{typeof(TEntity).Name} has a store-generated, complex or owned property, whose columns InsertMissing does not write. Add these rows through EF in the caller's context instead.");
        }

        var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
        var sql = dbContext.GetService<ISqlGenerationHelper>();

        return new MigrationInsert<TEntity>(
            sql.DelimitIdentifier(tableName, entityType.GetSchema()),
            properties,
            key.Properties,
            property => sql.DelimitIdentifier(property.GetColumnName(storeObject)
                ?? throw new InvalidOperationException($"{typeof(TEntity).Name}.{property.Name} has no column in {tableName}.")));
    }

    /// <summary>
    /// Runs one insert statement over a chunk of rows and returns the rows of that chunk it inserted.
    /// The statement reads parameters named @p0 upward, row after row in <see cref="Columns"/> order, and must return the <see cref="KeyColumns"/> of each row it inserted, in that order.
    /// It runs on the caller's open transaction, and throws when there is none or when a returned key matches no row of the chunk.
    /// </summary>
    public async Task<IReadOnlyList<TEntity>> Execute(ServiceControlDbContext dbContext, string sql, TEntity[] chunk, CancellationToken cancellationToken = default)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = (dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("InsertMissing must run inside the caller's transaction, so its rows commit with the checkpoint.")).GetDbTransaction();
        command.CommandText = sql;
        // A raw command starts at the provider's own 30 seconds, where SaveChangesAsync took the configured value.
        command.CommandTimeout = dbContext.Database.GetCommandTimeout() ?? command.CommandTimeout;

        var index = 0;
        foreach (var row in chunk)
        {
            foreach (var property in properties)
            {
                // The mapping applies the value converter and the provider type, as EF's own inserts do.
                command.Parameters.Add(property.GetRelationalTypeMapping().CreateParameter(command, $"@p{index++}", property.GetGetter().GetClrValue(row), property.IsNullable));
            }
        }

        var rowsByKey = new Dictionary<object?[], TEntity>(KeyEquality);
        foreach (var row in chunk)
        {
            // A key repeated exactly is one row to every database, so the first row given carries it.
            rowsByKey.TryAdd(KeyOf(row), row);
        }

        var inserted = new List<TEntity>(chunk.Length);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            object?[] key = [.. keyProperties.Select((property, column) => FromProvider(property, reader.GetValue(column)))];

            // A key can come back as a different CLR type than it went in as, a date column read back as
            // DateTime say, and a plain lookup would report that as a bare KeyNotFoundException.
            inserted.Add(rowsByKey.TryGetValue(key, out var row)
                ? row
                : throw new InvalidOperationException($"The {Table} insert returned the key [{string.Join(", ", key.Select(Describe))}], which matches no row in the batch that produced it. Compare it against the key types the batch carried: [{string.Join(", ", keyProperties.Select(property => property.ClrType.Name))}]."));
        }

        return inserted;
    }

    object?[] KeyOf(TEntity row) => [.. keyProperties.Select(property => property.GetGetter().GetClrValue(row))];

    static string Describe(object? value) => value is null ? "null" : $"{value} ({value.GetType().Name})";

    static object? FromProvider(IProperty property, object value) =>
        property.GetRelationalTypeMapping().Converter is { } converter ? converter.ConvertFromProvider(value) : value;

    // Exact, because the database decided which keys were one before the statement ran, and OUTPUT and RETURNING echo the value inserted.
    sealed class ExactKey : IEqualityComparer<object?[]>
    {
        public bool Equals(object?[]? x, object?[]? y) => x!.SequenceEqual(y!);

        public int GetHashCode(object?[] key)
        {
            var hash = new HashCode();

            foreach (var value in key)
            {
                hash.Add(value);
            }

            return hash.ToHashCode();
        }
    }
}
