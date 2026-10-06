namespace Particular.ServiceControl;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using global::ServiceControl.Infrastructure;
using global::ServiceControl.Persistence;
using Microsoft.Extensions.Configuration;
using Particular.LicensingComponent.Contracts;
using ServiceBus.Management.Infrastructure.Settings;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class ServiceControlErrorInstanceEnvironmentDataProvider(Settings settings, INotificationsDataStore notificationsDataStore, IConfiguration configuration) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData() =>
    [
        Value("Features.IntegratedServicePulse", () => Toggle(settings.EnableIntegratedServicePulse)),
        Value("Features.MessageEditing", () => Toggle(settings.AllowMessageEditing)),
        Value("Features.ExternalIntegrationsPublishing", () => Toggle(!settings.DisableExternalIntegrationsPublishing)),
        Value("Features.ForwardErrorMessages", () => Toggle(settings.ForwardErrorMessages)),
        Value("Features.ErrorIngestion", () => Toggle(settings.IngestErrorMessages)),
        Value("Features.ConfigurationValidation", () => Toggle(settings.ValidateConfiguration)),
        Deferred("Features.EmailNotifications", EmailNotifications),
        Value("ServicePulse.MonitoringUrl", ServicePulseMonitoringUrl),
        Value("ServicePulse.DefaultRoute", () => WhenServicePulseIntegrated(() => EnvironmentVariableSet(ServicePulseDefaultRouteVariable) ? "Custom" : "Default")),
        Value("ServicePulse.ShowPendingRetry", () => WhenServicePulseIntegrated(() => Toggle(settings.ServicePulseSettings.ShowPendingRetry))),
        Value("Host.VirtualDirectory", () => string.IsNullOrEmpty(settings.VirtualDirectory) ? "None" : "Configured"),
        Value("Logging.Providers", LoggingProviders),
        Value("Logging.Level", () => settings.LoggingSettings.LogLevel.ToString()),
        Value("Telemetry.OtlpMetrics", () => Toggle(OtlpEndpoint.Read(configuration) is not null)),
        Value("Retention.ErrorHours", () => Hours(settings.ErrorRetentionPeriod)),
        Value("Retention.EventsHours", () => Hours(settings.EventsRetentionPeriod))
    ];

    static string Toggle(bool enabled) => enabled ? "Enabled" : "Disabled";

    static string Hours(TimeSpan retentionPeriod) =>
        Math.Round(retentionPeriod.TotalHours, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture);

    async ValueTask<string> EmailNotifications(CancellationToken cancellationToken)
    {
        var notificationsSettings = await notificationsDataStore.LoadSettings(cancellationToken);

        if (notificationsSettings.Email.Enabled)
        {
            return "Enabled";
        }

        return string.IsNullOrWhiteSpace(notificationsSettings.Email.SmtpServer) ? "NotConfigured" : "Disabled";
    }

    string ServicePulseMonitoringUrl() => WhenServicePulseIntegrated(() =>
    {
        if (settings.ServicePulseSettings.MonitoringUrl is null)
        {
            return "Disabled";
        }

        return EnvironmentVariableSet(ServicePulseMonitoringUrlVariable) || EnvironmentVariableSet(ServicePulseLegacyMonitoringUrlsVariable) ? "Custom" : "Default";
    });

    string WhenServicePulseIntegrated(Func<string> readValue) => settings.EnableIntegratedServicePulse ? readValue() : NotApplicable;

    static bool EnvironmentVariableSet(string name) => Environment.GetEnvironmentVariable(name) is not null;

    static string LoggingProviders()
    {
        var providers = new[] { Loggers.NLog, Loggers.Seq, Loggers.Otlp }.Where(LoggerUtil.IsLoggingTo).ToArray();
        return providers.Length == 0 ? "None" : string.Join(",", providers);
    }

    const string NotApplicable = "NotApplicable";
    const string ServicePulseMonitoringUrlVariable = "MONITORING_URL";
    const string ServicePulseLegacyMonitoringUrlsVariable = "MONITORING_URLS";
    const string ServicePulseDefaultRouteVariable = "DEFAULT_ROUTE";
}
