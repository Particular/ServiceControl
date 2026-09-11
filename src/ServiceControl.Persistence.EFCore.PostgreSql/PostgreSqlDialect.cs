namespace ServiceControl.Persistence.EFCore.PostgreSql;

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ServiceControl.Persistence.EFCore.DbContexts;

abstract class PostgreSqlDialect
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

    // Not a ceiling like SQL Server's, which PostgreSQL is nowhere near: a fixed chunk keeps the text down to a full shape and a remainder, so the planner can cache both.
    protected const int MaxRowsPerStatement = 50;
}
