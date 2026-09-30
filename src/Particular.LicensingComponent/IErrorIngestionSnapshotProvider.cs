namespace Particular.LicensingComponent;

using ServiceControl.Infrastructure.Ingestion;

/// <summary>
/// The host's own error ingestion counters, registered by the primary instance so that the
/// licensing collector can read them in process the way it polls audit instances over their API.
/// </summary>
public interface IErrorIngestionSnapshotProvider
{
    IngestionCountersSnapshot GetSnapshot();
}
