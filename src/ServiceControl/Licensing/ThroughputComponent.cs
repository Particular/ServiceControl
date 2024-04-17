
namespace Particular.ServiceControl;

using global::ServiceControl.Infrastructure;
using global::ServiceControl.LicenseManagement;
using global::ServiceControl.Transports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Particular.ThroughputCollector.Persistence;
using Particular.ThroughputCollector.Shared;
using ServiceBus.Management.Infrastructure.Settings;
using ThroughputCollector;
using ServiceControlPersistence = global::ServiceControl.Persistence;

class ThroughputComponent : ServiceControlComponent
{
    public override void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder)
    {
        AddPersistence(settings, hostBuilder.Services);
        hostBuilder.AddThroughputCollector(
            TransportManifestLibrary.Find(settings.TransportType)?.Name ?? settings.TransportType,
            settings.ErrorQueue,
            settings.ServiceName,
            LicenseManager.FindLicense().Details.RegisteredTo,
            ServiceControlVersion.GetFileVersion(),
            transportCustomization.ThroughputQueryProvider);
    }

    public override void Setup(Settings settings, IComponentInstallationContext context, IHostApplicationBuilder hostBuilder)
    {
        context.CreateQueue(PlatformEndpointHelper.ServiceControlThroughputDataQueue);

        AddPersistence(settings, hostBuilder.Services);
        context.RegisterInstallationTask(serviceProvider => serviceProvider.GetRequiredService<IPersistenceInstaller>().Install());
    }

    static void AddPersistence(Settings settings, IServiceCollection services)
    {
        var persistenceTypeName = ServiceControlPersistence.PersistenceManifestLibrary.GetName(settings.PersistenceType);
        var componentPersistenceManifest = PersistenceManifestLibrary.Find(persistenceTypeName);

        var persistenceConfiguration = ServiceControlPersistence.PersistenceFactory.LoadComponentPersistence<IPersistenceConfiguration>(
            settings,
            componentPersistenceManifest.Location,
            componentPersistenceManifest.AssemblyPath,
            componentPersistenceManifest.TypeName);

        services.AddThroughputPersistence(persistenceConfiguration);
    }
}