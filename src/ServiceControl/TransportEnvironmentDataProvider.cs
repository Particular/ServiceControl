namespace Particular.ServiceControl;

using System.Collections.Generic;
using System.Linq;
using global::ServiceControl.Transports;
using Particular.LicensingComponent.Contracts;
using ServiceBus.Management.Infrastructure.Settings;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class TransportEnvironmentDataProvider(Settings settings, ITransportCustomization transportCustomization, TransportSettings transportSettings) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData() =>
    [
        Value("Transport.Type", () => TransportManifestLibrary.Find(settings.TransportType)?.Name ?? "Unknown"),
        .. transportCustomization.GetEnvironmentData(transportSettings).Select(datum => Value(datum.Key, datum.ReadValue))
    ];
}
