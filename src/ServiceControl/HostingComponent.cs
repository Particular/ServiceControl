
namespace Particular.ServiceControl
{
    using global::ServiceControl.CustomChecks.Internal;
    using global::ServiceControl.Operations;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class HostingComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder) =>
            hostBuilder.ConfigureServices(services =>
            {
                //TODO: Where should these go?
                services.AddCustomCheck<CriticalErrorCustomCheck>();
                services.AddCustomCheck<CheckRemotes>();
                services.AddCustomCheck<CheckFreeDiskSpace>();
                services.AddCustomCheck<FailedAuditImportCustomCheck>();
            });

        public override void Setup(Settings settings, IComponentSetupContext context)
        {
            context.CreateQueue(settings.ServiceName);
        }
    }
}