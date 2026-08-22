namespace ServiceControl.Persistence.EFCore.PostgreSql;

using System.Linq;

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

    public static string PartitionTables() => Convert(partitioned: true);

    public static string UnpartitionTables() => Convert(partitioned: false);

    static string Convert(bool partitioned)
    {
        var partitionBy = partitioned ? " PARTITION BY RANGE (created_on)" : string.Empty;

        return string.Concat(Tables.Select(table =>
            $"""
             CREATE TABLE {table}_tmp (LIKE {table} INCLUDING ALL);
             DROP TABLE {table};
             CREATE TABLE {table} (LIKE {table}_tmp INCLUDING ALL){partitionBy};
             DROP TABLE {table}_tmp;

             """));
    }
}
