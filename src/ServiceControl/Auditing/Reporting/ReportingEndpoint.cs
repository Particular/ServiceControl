namespace ServiceControl.Auditing.Reporting
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Configuration;
    using ServiceControl.CustomChecks;
    using ServiceControl.Transports;

    /// <summary>
    /// The send only NServiceBus endpoint a host on a dedicated audit database reports through. It
    /// carries two messages to the primary, custom check results and detected endpoints, which the
    /// primary already handles from the standalone audit instance. Send only claims no queue, so a
    /// worker keeps owning nothing.
    /// </summary>
    static class ReportingEndpoint
    {
        public static void Add(IServiceCollection services, Settings settings, ITransportCustomization transportCustomization, TransportSettings transportSettings)
        {
            var configuration = new EndpointConfiguration(settings.InstanceName);
            configuration.AssemblyScanner().Disable = true;

            transportCustomization.CustomizeAuditEndpoint(configuration, transportSettings);

            configuration.UseSerialization<SystemJsonSerializer>();
            configuration.SetDiagnosticsPath(settings.LoggingSettings.LogPath);

            if (AppEnvironment.RunningInContainer)
            {
                configuration.CustomDiagnosticsWriter((_, _) => Task.CompletedTask);
            }

            services.AddNServiceBusEndpoint(configuration);

            services.AddSingleton(new PrimaryQueue(transportCustomization.ToTransportQualifiedQueueName(settings.ServiceControlQueueAddress)));
            services.AddSingleton<ICustomCheckResultReporter, PrimaryCustomCheckResultReporter>();
            services.AddSingleton<IEndpointDetectionReporter, PrimaryEndpointDetectionReporter>();
        }
    }

    sealed record PrimaryQueue(string Address);
}
