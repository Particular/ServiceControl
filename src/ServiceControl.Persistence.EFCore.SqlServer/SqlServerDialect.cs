namespace ServiceControl.Persistence.EFCore.SqlServer;

using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;

abstract class SqlServerDialect
{
    /// <summary>
    /// The delimited, schema qualified name of the table an entity is mapped to. Every statement
    /// below names its target this way so that a configured schema reaches the raw SQL too.
    /// </summary>
    protected static string Table<TEntity>(ServiceControlDbContext dbContext) => SchemaQualifiedTableName.For<TEntity>(dbContext);

    /// <summary>
    /// How to declare each column of a statement's rows, taken from the properties the columns are
    /// mapped to. Pass the property names in the order the values sit in each row. A row may be
    /// shorter than this list, but not longer. A text column gets its length as its size, or -1 for nvarchar(max).
    /// </summary>
    protected static ColumnType[] ColumnTypes<TEntity>(ServiceControlDbContext dbContext, params string[] properties)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not part of the model.");

        return [.. properties.Select(name =>
        {
            var property = entityType.FindProperty(name)
                ?? throw new InvalidOperationException($"{typeof(TEntity).Name} has no property {name}.");

            var dbType = property.GetRelationalTypeMapping().DbType
                ?? throw new InvalidOperationException($"{typeof(TEntity).Name}.{name} has no DbType in its type mapping.");

            return new ColumnType(dbType, dbType == DbType.String ? property.GetMaxLength() ?? -1 : null);
        })];
    }

    protected readonly record struct ColumnType(DbType DbType, int? Size);

    protected static async Task Execute(ServiceControlDbContext dbContext, string sql, ColumnType[] columnTypes, IEnumerable<object?[]> rows, CancellationToken cancellationToken = default)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = (dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Dialect statements must run inside a transaction")).GetDbTransaction();
        command.CommandText = sql;

        var index = 0;
        foreach (var row in rows)
        {
            for (var column = 0; column < row.Length; column++)
            {
                var value = row[column];
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@p{index++}";
                parameter.Value = value ?? DBNull.Value;

                // Attempt de-duplication compares LastAttemptedAt for equality, so a datetime keeps
                // datetime2 precision wherever it sits, including the lone "now" that ResolveRetriedMessages sends first.
                if (value is DateTime)
                {
                    parameter.DbType = DbType.DateTime2;
                }
                else
                {
                    // SQL Server reuses a plan only when the declarations match, so every value, null or not,
                    // is declared by its column. A string longer than its column keeps its own length,
                    // because SqlClient would silently cut it and SQL Server must refuse it instead.
                    var (dbType, size) = columnTypes[column];
                    if (value is string && dbType != DbType.String)
                    {
                        throw new InvalidOperationException($"Parameter {parameter.ParameterName} holds text, but its column is declared as {dbType}.");
                    }

                    parameter.DbType = dbType;
                    if (size is { } columnSize)
                    {
                        parameter.Size = value is string text && columnSize != -1 && text.Length > columnSize ? text.Length : columnSize;
                    }
                }

                command.Parameters.Add(parameter);
            }
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    protected static string ParameterRows(int rowCount, int columnCount)
    {
        var sql = new StringBuilder();

        for (var row = 0; row < rowCount; row++)
        {
            sql.Append(row == 0 ? "(" : ",\n(");

            for (var column = 0; column < columnCount; column++)
            {
                if (column > 0)
                {
                    sql.Append(", ");
                }

                sql.Append("@p").Append((row * columnCount) + column);
            }

            sql.Append(')');
        }

        return sql.ToString();
    }

    protected static int MaxRowsPerStatement(int columns) => MaxParametersPerStatement / columns;

    // SQL Server's ceiling is 2100 (https://learn.microsoft.com/en-us/sql/sql-server/maximum-capacity-specifications-for-sql-server).
    // The 100 spare absorbs sp_executesql's own two and anything a statement carries outside its rows.
    const int MaxParametersPerStatement = 2000;
}
