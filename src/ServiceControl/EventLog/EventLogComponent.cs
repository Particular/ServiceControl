namespace ServiceControl.EventLog
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.DomainEvents;

    class EventLogComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddSingleton<EventLogMappings>();
            services.AddDomainEventHandler<AuditEventLogWriter>();
        }
    }
}