namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure;

using System.Globalization;

static class AuditBodyId
{
    const string HourFormat = "yyyyMMddHH";
    const string DayFormat = "yyyyMMdd";

    public static string Format(DateTime createdOn, Guid uniqueMessageId) =>
        $"{createdOn.ToString(HourFormat, CultureInfo.InvariantCulture)}-{uniqueMessageId}";

    // Ids start with the hour, so a day's bodies share one prefix and a partition drop can match them.
    public static string HourPrefix(DateTime hour) => $"{hour.ToString(HourFormat, CultureInfo.InvariantCulture)}-";

    public static string DayPrefix(DateTime day) => day.ToString(DayFormat, CultureInfo.InvariantCulture);

    public static bool TryParse(string bodyId, out DateTime createdOn, out Guid uniqueMessageId)
    {
        createdOn = default;
        uniqueMessageId = default;

        return bodyId.Length > HourFormat.Length
            && bodyId[HourFormat.Length] == '-'
            && DateTime.TryParseExact(bodyId[..HourFormat.Length], HourFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out createdOn)
            && Guid.TryParse(bodyId[(HourFormat.Length + 1)..], out uniqueMessageId);
    }
}
