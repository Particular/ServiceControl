namespace Particular.LicensingComponent.Contracts;

/// <summary>
/// Daily ingestion records kept by the hourly collector, per source (Error or Audit, the audit
/// side summed across instances). Never saved by versions that predate it, so a null read means
/// unknown and the report omits the keys.
/// </summary>
public record IngestionHistory(List<IngestionDay> Days)
{
    public const string ErrorSource = "Error";
    public const string AuditSource = "Audit";
}

/// <param name="Date">The UTC day, at midnight.</param>
/// <param name="PeakHourMessages">The busiest hour's messages.</param>
/// <param name="PeakHourBusySeconds">The time the ingestion loop spent processing batches during that busiest hour.</param>
/// <param name="PeakHourStorageSeconds">The time spent in storage writes during that busiest hour. Always zero for sources that do not measure storage separately.</param>
public record IngestionDay(
    DateTime Date,
    string Source,
    long Messages,
    long PeakHourMessages,
    double PeakHourBusySeconds,
    double PeakHourStorageSeconds,
    long LagOverOneMinuteMessages,
    long LagOverTenMinutesMessages,
    long LagOverSixtyMinutesMessages,
    long LagKnownMessages);
