namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Persistence.EFCore.DbContexts;

/// <summary>
/// The migration's provider-specific SQL. Statements run inside the caller's transaction and insert only what is absent.
/// </summary>
public interface IMigrationSqlDialect
{
    /// <summary>
    /// Reads, once, whatever the statements need from the schema. On SQL Server that is the collation of every
    /// string column of every primary key, composite keys included.
    /// </summary>
    Task Open(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// How the database compares one string key column, so a caller can work out which of its own rows the insert will treat as one. The insert compares in the database itself; this only predicts what it will find.
    /// Call <see cref="Open"/> first, and name a property that is a string column of the entity's primary key.
    /// </summary>
    IEqualityComparer<string> KeyComparer(Type entityType, string propertyName);

    /// <summary>
    /// How many rows the provider will take in one statement, for rows carrying this many values each.
    /// </summary>
    int RowsPerStatement(int parametersPerRow);

    /// <summary>
    /// Inserts the rows whose key the table does not hold, merges any whose keys the database treats as one, and returns the rows it inserted.
    /// </summary>
    Task<IReadOnlyList<TEntity>> InsertMissing<TEntity>(ServiceControlDbContext dbContext, IReadOnlyList<TEntity> rows, CancellationToken cancellationToken = default) where TEntity : class;
}
