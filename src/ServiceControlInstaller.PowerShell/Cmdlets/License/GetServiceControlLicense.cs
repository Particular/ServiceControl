namespace ServiceControlInstaller.PowerShell
{
    using System.Management.Automation;
    using Engine.LicenseMgmt;

    [Cmdlet(VerbsCommon.Get, "ServiceControlLicense")]
    public class GetServiceControlLicense : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            var license = LicenseManager.FindLicense();

            var p = new PSObject
            {
                Properties =
                {
                    new PSNoteProperty("Location", license.Location),
                    new PSNoteProperty("ExpirationDate", license.Details.ExpirationDate),
                    new PSNoteProperty("LicenseType", license.Details.LicenseType),
                    new PSNoteProperty("LicensedTo", license.Details.RegisteredTo),
                    new PSNoteProperty("UpgradeProtectionExpiration", license.Details.UpgradeProtectionExpiration),
                    new PSNoteProperty("TrialLicense", license.Details.IsExtendedTrial | license.Details.IsTrialLicense)
                },
                TypeNames = {"ServiceControlLicense.Information"}
            };
            WriteObject(p);
        }
    }
}