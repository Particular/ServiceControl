namespace ServiceControl.Persistence.BodyStorage;

using System.Globalization;

// Keys format the clock fields as supplied and never convert the time zone: EF can return Unspecified kinds.
// The fixed-width hour followed by '/' is what lets the day prefix match every hour of that day.
static class AuditBodyStorageKey
{
    public static string For(DateTime createdOn, string bodyId) =>
        string.Create(CultureInfo.InvariantCulture, $"{createdOn:yyyyMMddHH}/{bodyId}");

    public static string HourPrefix(DateTime hour) =>
        string.Create(CultureInfo.InvariantCulture, $"{hour:yyyyMMddHH}/");

    public static string DayPrefix(DateTime day) =>
        string.Create(CultureInfo.InvariantCulture, $"{day:yyyyMMdd}");
}
