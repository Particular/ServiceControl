namespace Particular.ServiceControl;

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using global::ServiceControl.Persistence;
using global::ServiceControl.Persistence.MessageRedirects;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.Persistence;
using ServiceBus.Management.Infrastructure.Settings;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class ErrorInstanceStoredChoicesEnvironmentDataProvider(
    Settings settings,
    IEndpointSettingsStore endpointSettingsStore,
    IEndpointInstanceMonitoring endpointInstanceMonitoring,
    IMessageRedirectsDataStore messageRedirectsDataStore,
    ILicensingDataStore licensingDataStore) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData()
    {
        Task<InstanceTracking> tracking = null;
        Task<InstanceTracking> Tracking(CancellationToken cancellationToken) => tracking ??= ReadInstanceTracking(cancellationToken);

        return
        [
            Deferred("Heartbeats.TrackInstancesDefault", async cancellationToken => (await Tracking(cancellationToken)).TrackByDefault ? "Enabled" : "Disabled"),
            Deferred("Heartbeats.TrackInstancesOverrides", async cancellationToken => Count((await Tracking(cancellationToken)).Overrides)),
            Value("Heartbeats.KnownInstances", () => Count(endpointInstanceMonitoring.GetEndpoints().Length)),
            Value("Heartbeats.MonitoredInstances", () => Count(endpointInstanceMonitoring.GetEndpoints().Count(endpoint => endpoint.Monitored))),
            Deferred("Recoverability.Redirects", async cancellationToken => Count((await messageRedirectsDataStore.GetRedirects(cancellationToken)).Count)),
            Deferred("Licensing.ReportMasks", async cancellationToken => Count((await licensingDataStore.GetReportMasks(cancellationToken)).Count))
        ];
    }

    async Task<InstanceTracking> ReadInstanceTracking(CancellationToken cancellationToken)
    {
        var endpointSettings = new List<EndpointSettings>();

        await foreach (var endpoint in endpointSettingsStore.GetAllEndpointSettings(cancellationToken))
        {
            endpointSettings.Add(endpoint);
        }

        var defaultRow = endpointSettings.FirstOrDefault(endpoint => endpoint.Name == string.Empty);
        var trackByDefault = defaultRow?.TrackInstances ?? settings.TrackInstancesInitialValue;
        var overrides = endpointSettings.Count(endpoint => endpoint.Name != string.Empty && endpoint.TrackInstances != trackByDefault);

        return new InstanceTracking(trackByDefault, overrides);
    }

    static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);

    record InstanceTracking(bool TrackByDefault, int Overrides);
}
