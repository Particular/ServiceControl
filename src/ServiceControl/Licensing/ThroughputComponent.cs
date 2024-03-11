
namespace Particular.ServiceControl;

using global::ServiceControl.LicenseManagement;
using global::ServiceControl.Persistence;
using global::ServiceControl.Transports;
using Microsoft.Extensions.Hosting;
using Particular.ThroughputCollector;
using ServiceBus.Management.Infrastructure.Settings;

class ThroughputComponent : ServiceControlComponent
{
    public override void Configure(Settings settings, IHostApplicationBuilder hostBuilder) =>
        hostBuilder.AddThroughputCollector(
            transportType: TransportManifestLibrary.GetName(settings.TransportType),
            serviceControlAPI: settings.ApiUrl,
            serviceControlQueue: settings.ServiceName,
            errorQueue: settings.ErrorQueue,
            auditQueue: "?",
            transportConnectionString: settings.TransportConnectionString,
            persistenceType: PersistenceManifestLibrary.GetName(settings.PersistenceType),
            customerName: LicenseManager.FindLicense().Details.RegisteredTo);

    public override void Setup(Settings settings, IComponentInstallationContext context) =>
        context.RegisterInstallationTask(() => ThroughputCollectorInstaller.Install(PersistenceManifestLibrary.GetName(settings.PersistenceType)));
}