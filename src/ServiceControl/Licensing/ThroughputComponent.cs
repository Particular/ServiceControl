
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
    public override void Configure(Settings settings, ITransportCustomization transportCustomization,
        IHostApplicationBuilder hostBuilder) =>
        hostBuilder.AddThroughputCollector(
            TransportManifestLibrary.Find(settings.TransportType)?.Name ?? settings.TransportType,
            settings.ErrorQueue,
            settings.ServiceName,
            PersistenceManifestLibrary.GetName(settings.PersistenceType),
            LicenseManager.FindLicense().Details.RegisteredTo,
            ServiceControlVersion.GetFileVersion(),
            transportCustomization.ThroughputQueryProvider);

    public override void Setup(Settings settings, IComponentInstallationContext context, IHostApplicationBuilder hostBuilder)
    {
        hostBuilder.AddThroughputCollectorPersistence(PersistenceManifestLibrary.GetName(settings.PersistenceType));
        context.RegisterInstallationTask(serviceProvider => serviceProvider.GetRequiredService<ThroughputPersistence.IPersistenceInstaller>().Install());
    }
}