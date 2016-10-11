namespace ServiceControl.Licensing
{
    using Nancy;
    using Particular.ServiceControl.Licensing;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class LicenseModule : BaseModule
    {
        public ActiveLicense ActiveLicense { get; set; }

        public LicenseModule()
        {
            Get["/license"] = parameters =>
            {
                var licenseInfo = new LicenseInfo
                {
                    TrialLicense = ActiveLicense.Details.IsTrialLicense,
                    Edition = ActiveLicense.Details.Edition ?? "" ,
                    RegisteredTo = ActiveLicense.Details.RegisteredTo ?? "",
                    UpgradeProtectionExpiration = ActiveLicense.Details.UpgradeProtectionExpiration?.ToString("O") ?? "",
                    ExpirationDate = ActiveLicense.Details.ExpirationDate?.ToString("O") ?? ""
                };
                return Negotiate.WithModel(licenseInfo);
            };
        }

        class LicenseInfo
        {
            public bool TrialLicense { get; set; }
            public string Edition { get; set; }
            public string RegisteredTo { get; set; }
            public string UpgradeProtectionExpiration { get; set; }
            public string ExpirationDate { get; set; }
        }
    }
}
