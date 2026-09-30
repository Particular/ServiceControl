namespace Particular.LicensingComponent.AuditThroughput;

using ServiceControl.Infrastructure.Ingestion;

/// <summary>
/// One audit instance's running counters. The ApiUri only keys hourly deltas in memory and is
/// never stored or reported.
/// </summary>
public record AuditIngestionSnapshot(string ApiUri, IngestionCountersSnapshot Snapshot);
