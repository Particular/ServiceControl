
namespace Particular.ServiceControl;

using System;
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
        IHostApplicationBuilder hostBuilder)
    {
        var persistenceManifest = PersistenceManifestLibrary.Find(settings.PersistenceType)
            ?? throw new InvalidOperationException($"No manifest found for {settings.PersistenceType} persistenceType");

        hostBuilder.AddThroughputCollector(
            TransportManifestLibrary.Find(settings.TransportType)?.Name ?? settings.TransportType,
            settings.ErrorQueue,
            settings.ServiceName,
            persistenceManifest.Name,
            persistenceManifest.AssemblyPath,
            LicenseManager.FindLicense().Details.RegisteredTo,
            ServiceControlVersion.GetFileVersion(),
            transportCustomization.ThroughputQueryProvider);
    }

    public override void Setup(Settings settings, IComponentInstallationContext context, IHostApplicationBuilder hostBuilder)
    {
        var persistenceManifest = PersistenceManifestLibrary.Find(settings.PersistenceType)
            ?? throw new InvalidOperationException($"No manifest found for {settings.PersistenceType} persistenceType");

        hostBuilder.AddThroughputCollectorPersistence(persistenceManifest.Name, persistenceManifest.AssemblyPath);
        context.RegisterInstallationTask(serviceProvider => serviceProvider.GetRequiredService<ThroughputPersistence.IPersistenceInstaller>().Install());
    }
}