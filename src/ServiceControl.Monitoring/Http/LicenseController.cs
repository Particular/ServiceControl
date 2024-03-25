namespace ServiceControl.Monitoring.Http
{
    using Licensing;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    public class LicenseController(ActiveLicense activeLicense) : ControllerBase
    {
        [Route("license")]
        [HttpGet]
        public ActionResult<LicenseInfo> License() =>
            new LicenseInfo
            {
                TrialLicense = activeLicense.Details.IsTrialLicense,
                Edition = activeLicense.Details.Edition ?? string.Empty,
                RegisteredTo = activeLicense.Details.RegisteredTo ?? string.Empty,
                UpgradeProtectionExpiration = activeLicense.Details.UpgradeProtectionExpiration?.ToString("O") ?? string.Empty,
                ExpirationDate = activeLicense.Details.ExpirationDate?.ToString("O") ?? string.Empty,
                Status = activeLicense.IsValid ? "valid" : "invalid"
            };

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