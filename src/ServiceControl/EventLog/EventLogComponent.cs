namespace ServiceControl.EventLog
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.DomainEvents;
    using Transports;

    class EventLogComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, EndpointConfiguration endpointConfiguration, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddSingleton<EventLogMappings>();
            services.AddDomainEventHandler<AuditEventLogWriter>();
        }
    }
}