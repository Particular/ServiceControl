namespace ServiceControl.Licensing
{
    using System.Web.Http;
    using Particular.ServiceControl.Licensing;
    using ServiceBus.Management.Infrastructure.Settings;

    class LicenseController : ApiController
    {
        public LicenseController(ActiveLicense activeLicense, Settings settings)
        {
            this.settings = settings;
            this.activeLicense = activeLicense;
        }

        [Route("license")]
        [HttpGet]
        public IHttpActionResult License(bool? refresh = null)
        {
            if (refresh == true)
            {
                activeLicense.Refresh();
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
                InstanceName = settings.ServiceName ?? string.Empty,
                LicenseStatus = activeLicense.Details.GetLicenseStatus().ToString()
            };

            return Ok(licenseInfo);
        }

        ActiveLicense activeLicense;
        Settings settings;

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