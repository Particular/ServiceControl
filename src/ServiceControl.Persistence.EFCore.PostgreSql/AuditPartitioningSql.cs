namespace ServiceControl.Persistence.EFCore.PostgreSql;

using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

/// <summary>
/// Converts the two audit tables to range partitioned on created_on. EF Core cannot express
/// declarative partitioning and PostgreSQL cannot convert a plain table in place, so the table is
/// cloned into a partitioned one.
/// </summary>
/// <remarks>
/// The clone is <c>LIKE ... INCLUDING ALL</c> rather than a hand written column list, so the columns,
/// primary key and indexes stay whatever EF generated and cannot drift from the model. This runs
/// after the CreateTable and CreateIndex calls in the migration, so there is something to clone.
/// A partitioned table's primary key must include the partition key, which is why the audit keys are
/// composite.
/// </remarks>
static class AuditPartitioningSql
{
    static readonly string[] Tables = ["audit_messages", "saga_snapshots"];

    public static readonly string PartitionTables = Convert(null, partitioned: true);

    public static readonly string UnpartitionTables = Convert(null, partitioned: false);

    /// <summary>
    /// Re-renders the statement the migration carries with the configured schema in it, the way
    /// FullTextSearchSql does for the full text index.
    /// </summary>
    public static MigrationOperation Rewrite(SqlOperation operation, string schema) =>
        operation.Sql switch
        {
            var sql when sql == PartitionTables => WithSql(operation, Convert(schema, partitioned: true)),
            var sql when sql == UnpartitionTables => WithSql(operation, Convert(schema, partitioned: false)),
            _ => operation
        };

    public static bool IsHandled(string sql) => sql == PartitionTables || sql == UnpartitionTables;

    static string Convert(string? schema, bool partitioned)
    {
        var partitionBy = partitioned ? " PARTITION BY RANGE (created_on)" : string.Empty;

        return string.Concat(Tables.Select(table =>
        {
            var name = Qualify(schema, table);
            var clone = Qualify(schema, $"{table}_tmp");

            return $"""
                CREATE TABLE {clone} (LIKE {name} INCLUDING ALL);
                DROP TABLE {name};
                CREATE TABLE {name} (LIKE {clone} INCLUDING ALL){partitionBy};
                DROP TABLE {clone};

                """;
        }));
    }

    static string Qualify(string? schema, string name) => schema is null ? name : $"\"{schema}\".{name}";

    static SqlOperation WithSql(SqlOperation operation, string sql) =>
        new() { Sql = sql, SuppressTransaction = operation.SuppressTransaction };
}
