namespace ServiceControl.Licensing
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Monitoring.HeartbeatMonitoring;
    using Particular.ServiceControl.Licensing;
    using ServiceBus.Management.Infrastructure.Settings;

    [ApiController]
    [Route("api")]
    public class LicenseController(ActiveLicense activeLicense, Settings settings, MassTransitConnectorHeartbeatStatus connectorHeartbeatStatus) : ControllerBase
    {
        [HttpGet]
        [Route("license")]
        public async Task<ActionResult<LicenseInfo>> License(bool refresh, string clientName, CancellationToken cancellationToken)
        {
            if (refresh)
            {
                await activeLicense.Refresh(cancellationToken);
            }

            var licenseInfo = new LicenseInfo
            {
                TrialLicense = activeLicense.Details.IsTrialLicense,
                Edition = activeLicense.Details.Edition ?? string.Empty,
                RegisteredTo = activeLicense.Details.RegisteredTo ?? string.Empty,
                UpgradeProtectionExpiration = activeLicense.Details.UpgradeProtectionExpiration?.ToString("O") ?? string.Empty,
                ExpirationDate = activeLicense.Details.ExpirationDate?.ToString("O") ?? string.Empty,
                Status = activeLicense.IsValid ? "valid" : "invalid",
                LicenseType = activeLicense.Details.LicenseType ?? string.Empty,
                InstanceName = settings.ServiceControl.InstanceName ?? string.Empty,
                LicenseStatus = activeLicense.Details.Status,
                LicenseExtensionUrl = connectorHeartbeatStatus.LastHeartbeat == null
                    ? $"https://particular.net/extend-your-trial?p={clientName}"
                    : $"https://particular.net/license/mt?p={clientName}&t={(activeLicense.IsEvaluation ? 0 : 1)}"
            };

            return licenseInfo;
        }

        public class LicenseInfo
        {
            public bool TrialLicense { get; set; }

            public string Edition { get; set; }

            public string RegisteredTo { get; set; }

            public string UpgradeProtectionExpiration { get; set; }

            public string ExpirationDate { get; set; }

            public string Status { get; set; }

            public string LicenseType { get; set; }

            public string InstanceName { get; set; }

            public string LicenseStatus { get; set; }

            public string LicenseExtensionUrl { get; set; }
        }
    }
}