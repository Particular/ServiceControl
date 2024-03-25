
namespace Particular.ServiceControl;

using global::ServiceControl.Infrastructure;
using global::ServiceControl.LicenseManagement;
using global::ServiceControl.Persistence;
using global::ServiceControl.Transports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ThroughputCollector;
using ThroughputPersistence = ThroughputCollector.Persistence;

class ThroughputComponent : ServiceControlComponent
{
    public override void Configure(Settings settings, IHostApplicationBuilder hostBuilder) =>
        hostBuilder.AddThroughputCollector(
            transportType: TransportManifestLibrary.GetName(settings.TransportType),
            serviceControlQueue: settings.ServiceName,
            errorQueue: settings.ErrorQueue,
            persistenceType: PersistenceManifestLibrary.GetName(settings.PersistenceType),
            customerName: LicenseManager.FindLicense().Details.RegisteredTo,
            serviceControlVersion: ServiceControlVersion.GetFileVersion());

    public override void ConfigureInstallation(Settings settings, IHostApplicationBuilder hostBuilder) =>
        hostBuilder.AddThroughputCollectorPersistence(PersistenceManifestLibrary.GetName(settings.PersistenceType));

    public override void Setup(Settings settings, IComponentInstallationContext context) =>
        context.RegisterInstallationTask(serviceProvider => serviceProvider.GetRequiredService<ThroughputPersistence.IPersistenceInstaller>().Install());
}