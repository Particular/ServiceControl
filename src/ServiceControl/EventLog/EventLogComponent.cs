namespace ServiceControl.EventLog
{
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class EventLogComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddSingleton<EventLogMappings>();
            services.AddDomainEventHandler<AuditEventLogWriter>();
        }
    }
}