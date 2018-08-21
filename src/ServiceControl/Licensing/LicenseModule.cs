namespace ServiceControl.Licensing
{
    using System;
    using Nancy;
    using Particular.ServiceControl.Licensing;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class LicenseModule : BaseModule
    {
        public LicenseModule()
        {
            Get["/license"] = parameters =>
            {
                if (string.Equals(Request.Query["refresh"], "true", StringComparison.InvariantCultureIgnoreCase))
                {
                    ActiveLicense.Refresh();
                }

                var licenseInfo = new LicenseInfo
                {
                    TrialLicense = ActiveLicense.Details.IsTrialLicense,
                    Edition = ActiveLicense.Details.Edition ?? string.Empty,
                    RegisteredTo = ActiveLicense.Details.RegisteredTo ?? string.Empty,
                    UpgradeProtectionExpiration = ActiveLicense.Details.UpgradeProtectionExpiration?.ToString("O") ?? string.Empty,
                    ExpirationDate = ActiveLicense.Details.ExpirationDate?.ToString("O") ?? string.Empty,
                    Status = ActiveLicense.IsValid ? "valid" : "invalid"
                };
                return Negotiate.WithModel(licenseInfo);
            };
        }

        public ActiveLicense ActiveLicense { get; set; }

        class LicenseInfo
        {
            public bool TrialLicense { get; set; }
            public string Edition { get; set; }
            public string RegisteredTo { get; set; }
            public string UpgradeProtectionExpiration { get; set; }
            public string ExpirationDate { get; set; }
            public string Status { get; set; }
        }
    }
}