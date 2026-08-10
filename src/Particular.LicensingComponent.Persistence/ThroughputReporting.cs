namespace Particular.LicensingComponent.Persistence;

public static class ThroughputReporting
{
    /// <summary>
    /// How far back a throughput report reaches. Throughput reads are filtered to this window, so it
    /// has to be the same wherever <see cref="ILicensingDataStore"/> is implemented.
    /// </summary>
    public const int ReportedMonths = 14;
}
