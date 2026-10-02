namespace ServiceControl.Audit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Infrastructure.Ingestion;
    using static ServiceControl.Audit.Persistence.EnvironmentDatum;

    class IngestionEnvironmentDataProvider(IngestionCounters counters, TimeProvider timeProvider) : IEnvironmentDataProvider
    {
        public IEnumerable<EnvironmentDatum> GetData() =>
            IngestionSummary.Describe(counters.GetSnapshot(), timeProvider.GetUtcNow().UtcDateTime, "Ingestion", "Health", includeStorage: false)
                .Select(pair => Value(pair.Key, () => pair.Value));
    }
}
