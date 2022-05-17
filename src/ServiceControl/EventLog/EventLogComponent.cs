namespace ServiceControl.EventLog
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;

    class EventLogComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<EventLogMappings>();
                services.AddSingleton<AuditEventLogWriter>();
            });
        }

        public override void Setup(Settings settings, IComponentSetupContext context)
        {
        }
    }
}