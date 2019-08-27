namespace ServiceControl.Monitoring.Http
{
    using Licensing;
    using Nancy;

    public class LicenseApiModule : BaseModule
    {
        public LicenseApiModule()
        {
            Get["/license"] = _ => Negotiate.WithModel(new
            {
                TrialLicense = LicenseManager.Details.IsTrialLicense,
                Edition = LicenseManager.Details.Edition ?? string.Empty,
                RegisteredTo = LicenseManager.Details.RegisteredTo ?? string.Empty,
                UpgradeProtectionExpiration = LicenseManager.Details.UpgradeProtectionExpiration?.ToString("O") ?? string.Empty,
                ExpirationDate = LicenseManager.Details.ExpirationDate?.ToString("O") ?? string.Empty,
                Status = LicenseManager.IsValid ? "valid" : "invalid"
            });
        }
    }
}