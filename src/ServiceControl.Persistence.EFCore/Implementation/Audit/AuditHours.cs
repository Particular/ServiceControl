namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

/// <summary>
/// The hour is the unit audit storage is organised by: every row is stamped with the hour it was
/// ingested in, PostgreSQL partitions the audit tables by it, external bodies are keyed by it, and
/// retention drops it whole.
/// </summary>
public static class AuditHours
{
    /// <summary>
    /// How far ahead partitions are provisioned. Long, because only the retention owner provisions
    /// them and an ingestion worker cannot insert into an hour nobody provisioned.
    /// </summary>
    public static readonly TimeSpan Lookahead = TimeSpan.FromHours(48);

    public static DateTime Truncate(DateTime utc) => new(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);

    public static string PartitionName(string tableName, DateTime hour) => $"{tableName}_{hour:yyyyMMddHH}";
}
