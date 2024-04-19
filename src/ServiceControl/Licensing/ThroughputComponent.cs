namespace Particular.ServiceControl;

using global::ServiceControl.Infrastructure;
using global::ServiceControl.LicenseManagement;
using global::ServiceControl.Transports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ThroughputCollector;
using ThroughputCollector.Persistence;
using ThroughputCollector.Shared;

class ThroughputComponent : ServiceControlComponent
{
    public override void Configure(Settings settings, ITransportCustomization transportCustomization,
        IHostApplicationBuilder hostBuilder)
    {
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

        context.RegisterInstallationTask(serviceProvider =>
            serviceProvider.GetRequiredService<IPersistenceInstaller>().Install());
    }
}