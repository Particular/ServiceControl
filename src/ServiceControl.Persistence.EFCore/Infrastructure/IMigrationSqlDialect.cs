namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

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
    /// The length of a text value as the database counts it when checking a column's maximum length.
    /// </summary>
    int TextLength(string value);

    /// <summary>
    /// Finds a character the database refuses to store in any text column, which would fail the whole statement the value goes into.
    /// Returns that character, or null when the value has none.
    /// </summary>
    char? RejectedCharacterIn(string value);

    /// <summary>
    /// Inserts the rows whose key the table does not hold, merges any whose keys the database treats as one, and returns the rows it inserted.
    /// </summary>
    Task<IReadOnlyList<TEntity>> InsertMissing<TEntity>(ServiceControlDbContext dbContext, IReadOnlyList<TEntity> rows, CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// Sets each day's message count: a day the table does not hold is inserted, and a day it holds gets its count replaced, never added to.
    /// The rows go to the database in the order given. Every row's endpoint must already be in the table, or the foreign key fails the statement.
    /// Two rows with the same key in one call are not allowed on PostgreSQL, which refuses the statement. SQL Server keeps the last of them, which also covers names that differ only by a trailing space.
    /// Returns how many rows the statements inserted or updated. It runs on the caller's open transaction, and throws <see cref="InvalidOperationException"/> when there is none or when a returned key matches no row given.
    /// </summary>
    Task<int> SetLicensingEndpointThroughput(ServiceControlDbContext dbContext, IReadOnlyList<LicensingEndpointThroughputEntity> rows, CancellationToken cancellationToken = default);
}
