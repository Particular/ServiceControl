namespace Particular.ServiceControl;

using System;
using System.Collections.Generic;
using System.Linq;
using global::ServiceControl.Infrastructure.Ingestion;
using Particular.LicensingComponent.Contracts;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class ErrorIngestionEnvironmentDataProvider(IngestionCounters counters, TimeProvider timeProvider) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData() =>
        IngestionSummary.Describe(counters.GetSnapshot(), timeProvider.GetUtcNow().UtcDateTime, "Ingestion.Error", "Health.Error", includeStorage: true)
            .Select(pair => Value(pair.Key, () => pair.Value));
}
