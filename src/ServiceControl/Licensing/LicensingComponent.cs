namespace Particular.ServiceControl;

using global::ServiceControl.Infrastructure;
using global::ServiceControl.LicenseManagement;
using global::ServiceControl.Transports;
using Microsoft.Extensions.Hosting;
using Particular.LicensingComponent;
using Particular.LicensingComponent.Shared;
using ServiceBus.Management.Infrastructure.Settings;

class LicensingComponent : ServiceControlComponent
{
    public override void Configure(Settings settings, ITransportCustomization transportCustomization,
        IHostApplicationBuilder hostBuilder)
    {
        var licenseDetails = LicenseManager.FindLicense().Details;
        hostBuilder.AddLicensingComponent(
            TransportManifestLibrary.Find(settings.TransportType)?.Name ?? settings.TransportType,
            settings.ErrorQueue,
            settings.InstanceName,
            licenseDetails.RegisteredTo,
            ServiceControlVersion.GetFileVersion());
    }

    public override void Setup(Settings settings, IComponentInstallationContext context,
        IHostApplicationBuilder hostBuilder) =>
        context.CreateQueue(settings.ServiceControlSettings.ServiceControlThroughputDataQueue);
}