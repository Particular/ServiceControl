namespace ServiceControl.Licensing
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Particular.ServiceControl.Licensing;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;

    [ApiController]
    [Route("api")]
    public class LicenseController(ActiveLicense activeLicense, ITrialLicenseMetadataProvider licenseMetadataProvider, Settings settings) : ControllerBase
    {
        [HttpGet]
        [Route("license")]
        public async Task<ActionResult<LicenseInfo>> License(bool refresh, CancellationToken cancellationToken)
        {
            if (refresh)
            {
                await activeLicense.Refresh(licenseMetadataProvider, cancellationToken);
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
                InstanceName = settings.InstanceName ?? string.Empty,
                LicenseStatus = activeLicense.Details.Status
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
        }
    }
}