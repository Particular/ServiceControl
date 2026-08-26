namespace Particular.ServiceControl;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using global::ServiceControl.Notifications;
using global::ServiceControl.Persistence;
using Particular.LicensingComponent.Contracts;
using ServiceBus.Management.Infrastructure.Settings;

class ServiceControlErrorInstanceEnvironmentDataProvider(Settings settings, INotificationsDataStore notificationsDataStore) : IEnvironmentDataProvider
{
    public async Task<IEnumerable<(string key, string value)>> GetData(CancellationToken cancellationToken = default)
    {
        var notificationsSettings = await notificationsDataStore.LoadSettings(cancellationToken);

        return
        [
            ("Security.Authentication", Toggle(settings.OpenIdConnectSettings.Enabled)),
            ("Security.RoleBasedAuthorization", Toggle(settings.OpenIdConnectSettings.RoleBasedAuthorizationEnabled)),
            ("Security.Https", Toggle(settings.HttpsSettings.Enabled)),
            ("Features.IntegratedServicePulse", Toggle(settings.EnableIntegratedServicePulse)),
            ("Features.MessageEditing", Toggle(settings.AllowMessageEditing)),
            ("Features.ExternalIntegrationsPublishing", Toggle(!settings.DisableExternalIntegrationsPublishing)),
            ("Features.ForwardErrorMessages", Toggle(settings.ForwardErrorMessages)),
            ("Features.EmailNotifications", EmailNotifications(notificationsSettings)),
            ("Retention.ErrorHours", Hours(settings.ErrorRetentionPeriod)),
            ("Retention.AuditHours", Hours(settings.AuditRetentionPeriod)),
            ("Retention.EventsHours", Hours(settings.EventsRetentionPeriod))
        ];
    }

    static string Toggle(bool enabled) => enabled ? "Enabled" : "Disabled";

    static string Hours(TimeSpan? retentionPeriod) =>
        retentionPeriod is { } period
            ? Math.Round(period.TotalHours, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture)
            : "NotSet";

    static string EmailNotifications(NotificationsSettings notificationsSettings)
    {
        if (notificationsSettings.Email.Enabled)
        {
            return "Enabled";
        }

        return string.IsNullOrWhiteSpace(notificationsSettings.Email.SmtpServer) ? "NotConfigured" : "Disabled";
    }
}
