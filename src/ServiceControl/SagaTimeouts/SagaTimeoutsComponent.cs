namespace ServiceControl.SagaTimeouts
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    // See docs/saga-timeout-management-design.md. The data store comes from the persister.
    class SagaTimeoutsComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder) =>
            hostBuilder.Services.AddSingleton<SagaTimeoutReportProcessor>();
    }
}
