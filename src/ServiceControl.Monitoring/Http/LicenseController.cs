namespace ServiceControl.Monitoring.Http
{
    using Licensing;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    public class LicenseController(LicenseManager licenseManager) : ControllerBase
    {
        [Route("license")]
        [HttpGet]
        public ActionResult<LicenseInfo> License()
        {
            licenseManager.Refresh();

            return new LicenseInfo
            {
                TrialLicense = licenseManager.Details.IsTrialLicense,
                Edition = licenseManager.Details.Edition ?? string.Empty,
                RegisteredTo = licenseManager.Details.RegisteredTo ?? string.Empty,
                UpgradeProtectionExpiration = licenseManager.Details.UpgradeProtectionExpiration?.ToString("O") ?? string.Empty,
                ExpirationDate = licenseManager.Details.ExpirationDate?.ToString("O") ?? string.Empty,
                Status = licenseManager.IsValid ? "valid" : "invalid"
            };
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