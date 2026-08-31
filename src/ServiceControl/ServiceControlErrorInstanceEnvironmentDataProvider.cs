namespace Particular.ServiceControl;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using global::ServiceControl.Persistence;
using Particular.LicensingComponent.Contracts;
using ServiceBus.Management.Infrastructure.Settings;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class ServiceControlErrorInstanceEnvironmentDataProvider(Settings settings, INotificationsDataStore notificationsDataStore) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData() =>
    [
        Value("Security.Authentication", () => Toggle(settings.OpenIdConnectSettings.Enabled)),
        Value("Security.RoleBasedAuthorization", () => Toggle(settings.OpenIdConnectSettings.RoleBasedAuthorizationEnabled)),
        Value("Security.Https", () => Toggle(settings.HttpsSettings.Enabled)),
        Value("Features.IntegratedServicePulse", () => Toggle(settings.EnableIntegratedServicePulse)),
        Value("Features.MessageEditing", () => Toggle(settings.AllowMessageEditing)),
        Value("Features.ExternalIntegrationsPublishing", () => Toggle(!settings.DisableExternalIntegrationsPublishing)),
        Value("Features.ForwardErrorMessages", () => Toggle(settings.ForwardErrorMessages)),
        Deferred("Features.EmailNotifications", EmailNotifications),
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
}
