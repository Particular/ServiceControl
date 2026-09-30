namespace Particular.ServiceControl;

using global::ServiceControl.Infrastructure.Ingestion;
using Particular.LicensingComponent;

class ErrorIngestionSnapshotProvider(IngestionCounters counters) : IErrorIngestionSnapshotProvider
{
    public IngestionCountersSnapshot GetSnapshot() => counters.GetSnapshot();
}
