namespace ServiceControl.Auditing
{
    using System;
    using System.Diagnostics;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Resources;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Auditing.Metrics;
    using ServiceControl.Infrastructure;

    static class AuditIngestionOpenTelemetry
    {
        public static void AddAuditIngestionOpenTelemetry(this IHostApplicationBuilder builder, Settings settings)
        {
            if (string.IsNullOrEmpty(settings.OtlpEndpointUrl))
            {
                return;
            }

            if (!Uri.TryCreate(settings.OtlpEndpointUrl, UriKind.Absolute, out var otelMetricsUri))
            {
                throw new UriFormatException($"Invalid OtlpEndpointUrl: {settings.OtlpEndpointUrl}");
            }

            var version = FileVersionInfo.GetVersionInfo(typeof(AuditIngestionOpenTelemetry).Assembly.Location).ProductVersion;

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(b => b.AddService(
                    serviceName: settings.InstanceName,
                    serviceVersion: version,
                    autoGenerateServiceInstanceId: true))
                .WithMetrics(b =>
                {
                    b.AddAuditIngestionMetrics();
                    b.AddOtlpExporter(e => e.Endpoint = otelMetricsUri);
                    if (Debugger.IsAttached)
                    {
                        b.AddConsoleExporter();
                    }
                });

            var logger = LoggerUtil.CreateStaticLogger(typeof(AuditIngestionOpenTelemetry), settings.LoggingSettings.LogLevel);
            logger.LogInformation("OpenTelemetry metrics exporter enabled: {OtlpEndpointUrl}", settings.OtlpEndpointUrl);
        }
    }
}
