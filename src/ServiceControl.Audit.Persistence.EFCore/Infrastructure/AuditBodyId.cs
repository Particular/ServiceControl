namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure;

using System.Globalization;

static class AuditBodyId
{
    const string HourFormat = "yyyyMMddHH";

    public static string Format(DateTime createdOn, Guid uniqueMessageId) =>
        $"{createdOn.ToString(HourFormat, CultureInfo.InvariantCulture)}-{uniqueMessageId}";

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
