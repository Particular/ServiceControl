
namespace Particular.ServiceControl;

using global::ServiceControl.Persistence;
using global::ServiceControl.Transports;
using Microsoft.Extensions.Hosting;
using Particular.ThroughputCollector;
using ServiceBus.Management.Infrastructure.Settings;

class ThroughputComponent : ServiceControlComponent
{
    public override void Configure(Settings settings, IHostApplicationBuilder hostBuilder) =>
        hostBuilder.AddThroughputCollector(
            broker: TransportManifestLibrary.GetName(settings.TransportType),
            serviceControlAPI: settings.ApiUrl,
            errorQueue: settings.ErrorQueue,
            auditQueue: "?",
            transportConnectionString: settings.TransportConnectionString,
            persistenceType: PersistenceManifestLibrary.GetName(settings.PersistenceType));

    public override void Setup(Settings settings, IComponentInstallationContext context) =>
        context.RegisterInstallationTask(() => ThroughputCollectorInstaller.Install(PersistenceManifestLibrary.GetName(settings.PersistenceType)));
}