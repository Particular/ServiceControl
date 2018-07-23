// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace ServiceControlInstaller.PowerShell
{
    using System.Management.Automation;
    using Engine.UrlAcl;

    [Cmdlet(VerbsCommon.Remove, "UrlAcl")]
    public class RemoveUrlAcl : PSCmdlet
    {
        [ValidateNotNull]
        [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0, HelpMessage = "Specify the URLACL to remove")]
        public UrlReservation[] UrlAcl { get; set; }

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            foreach (var entry in UrlAcl)
            {
                UrlReservation.Delete(entry);
            }
        }
    }
}