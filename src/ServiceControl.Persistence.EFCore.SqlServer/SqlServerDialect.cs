namespace ServiceControl.Persistence.EFCore.SqlServer;

using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ServiceControl.Persistence.EFCore.DbContexts;

abstract class SqlServerDialect
{
    protected static async Task Execute(ServiceControlDbContext dbContext, string sql, IEnumerable<object?[]> rows, CancellationToken cancellationToken = default)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = (dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Dialect statements must run inside a transaction")).GetDbTransaction();
        command.CommandText = sql;

        var index = 0;
        foreach (var row in rows)
        {
            foreach (var value in row)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@p{index++}";
                parameter.Value = value ?? DBNull.Value;

                // Attempt de-duplication compares LastAttemptedAt for equality, so datetime
                // parameters must keep datetime2 precision instead of the datetime default.
                if (value is DateTime)
                {
                    parameter.DbType = DbType.DateTime2;
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

    // sharedParameters is for a statement that also carries values of its own, outside the per-row ones.
    protected static int MaxRowsPerStatement(int columns, int sharedParameters = 0) =>
        (MaxSqlParameters - ExecuteSqlOverhead - sharedParameters) / columns;

    // https://learn.microsoft.com/en-us/sql/sql-server/maximum-capacity-specifications-for-sql-server
    const int MaxSqlParameters = 2100;

    // The client sends every parameterised command through sp_executesql, which spends two of the 2100 on the statement and the parameter list.
    const int ExecuteSqlOverhead = 2;
}
