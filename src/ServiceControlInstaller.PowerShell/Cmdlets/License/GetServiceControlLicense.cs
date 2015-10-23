namespace ServiceControlInstaller.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.LicenseMgmt;

    [Cmdlet(VerbsCommon.Get, "ServiceControlLicense")]
    public class GetServiceControlLicense : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            var licenses = LicenseManager.FindLicenses().ToArray();
            if (licenses.Length == 0)
            {
                return;
            }
            foreach (var license in licenses)
            {
                var p = new PSObject
                {
                    Properties =
                    {
                        new PSNoteProperty( "Location", license.Location),
                        new PSNoteProperty( "ExpirationDate", license.Details.ExpirationDate),
                        new PSNoteProperty( "LicenseType", license.Details.LicenseType),
                        new PSNoteProperty( "LicensedTo", license.Details.RegisteredTo),
                        new PSNoteProperty( "UpgradeProtectionExpiration", license.Details.UpgradeProtectionExpiration),
                        new PSNoteProperty( "Valid", license.Details.Valid),
                        new PSNoteProperty( "TrialLicense", license.Details.IsExtendedTrial | license.Details.IsTrialLicense)
                    },
                    TypeNames = { "ServiceControlLicense.Information" }
                };
                WriteObject(p);
            }
        }
    }
}