namespace ServiceControl.Monitoring.Http
{
    using System.Web.Http;
    using Licensing;

    public class LicenseController : ApiController
    {
        internal LicenseController(LicenseManager licenseManager)
        {
            this.licenseManager = licenseManager;
        }

        [Route("license")]
        [HttpGet]
        public IHttpActionResult License()
        {
            licenseManager.Refresh();

            return Ok(new LicenseInfo
            {
                TrialLicense = licenseManager.Details.IsTrialLicense,
                Edition = licenseManager.Details.Edition ?? string.Empty,
                RegisteredTo = licenseManager.Details.RegisteredTo ?? string.Empty,
                UpgradeProtectionExpiration = licenseManager.Details.UpgradeProtectionExpiration?.ToString("O") ?? string.Empty,
                ExpirationDate = licenseManager.Details.ExpirationDate?.ToString("O") ?? string.Empty,
                Status = licenseManager.IsValid ? "valid" : "invalid"
            });
        }

        LicenseManager licenseManager;

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