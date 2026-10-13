namespace ServiceControl.Licensing;

using System;
using System.Threading;
using System.Threading.Tasks;
using Monitoring.HeartbeatMonitoring;
using Particular.ServiceControl.Licensing;
using ServiceBus.Management.Infrastructure.Settings;

public interface ILicenseInfoProvider
{
    Task<LicenseInfo> GetLicense(bool refresh, string clientName, CancellationToken cancellationToken = default);
}

sealed class LicenseInfoProvider(ActiveLicense activeLicense, Settings settings, MassTransitConnectorHeartbeatStatus connectorHeartbeatStatus) : ILicenseInfoProvider
{
    public async Task<LicenseInfo> GetLicense(bool refresh, string clientName, CancellationToken cancellationToken = default)
    {
        if (refresh)
        {
            await activeLicense.Refresh(cancellationToken);
        }

        var details = activeLicense.Details ?? throw new InvalidOperationException("License details are unavailable.");

        return new LicenseInfo
        {
            TrialLicense = details.IsTrialLicense,
            Edition = details.Edition ?? string.Empty,
            RegisteredTo = details.RegisteredTo ?? string.Empty,
            UpgradeProtectionExpiration = details.UpgradeProtectionExpiration?.ToString("O") ?? string.Empty,
            ExpirationDate = details.ExpirationDate?.ToString("O") ?? string.Empty,
            Status = activeLicense.IsValid ? "valid" : "invalid",
            LicenseType = details.LicenseType ?? string.Empty,
            InstanceName = settings.InstanceName ?? string.Empty,
            LicenseStatus = details.Status,
            Products = details.Products,
            HasEndpointMetadata = details.HasEndpointMetadata,
            LicenseExtensionUrl = connectorHeartbeatStatus.LastHeartbeat == null
                ? $"https://particular.net/extend-your-trial?p={clientName}"
                : $"https://particular.net/license/mt?p={clientName}&t={(activeLicense.IsEvaluation ? 0 : 1)}"
        };
    }
}