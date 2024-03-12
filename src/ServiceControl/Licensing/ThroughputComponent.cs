
namespace Particular.ServiceControl;

using global::ServiceControl.LicenseManagement;
using global::ServiceControl.Transports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Particular.ThroughputCollector;
using ServiceBus.Management.Infrastructure.Settings;

using ServiceControlPersistence = global::ServiceControl.Persistence;
using ThroughputPersistence = ThroughputCollector.Persistence;

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
            persistenceType: ServiceControlPersistence.PersistenceManifestLibrary.GetName(settings.PersistenceType),
            customerName: LicenseManager.FindLicense().Details.RegisteredTo);

    public override void ConfigureInstallation(Settings settings, IHostApplicationBuilder hostBuilder) => Configure(settings, hostBuilder);

    public override void Setup(Settings settings, IComponentInstallationContext context) =>
        context.RegisterInstallationTask(serviceProvider => serviceProvider.GetRequiredService<ThroughputPersistence.IPersistenceInstaller>().Install());
}