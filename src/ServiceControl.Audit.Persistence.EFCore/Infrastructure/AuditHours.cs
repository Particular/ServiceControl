namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure;

static class AuditHours
{
    public static readonly TimeSpan Lookahead = TimeSpan.FromHours(48);

    public static DateTime Truncate(DateTime utc) => new(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
}
