// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace ServiceControlInstaller.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using System.Security.Principal;

    [Cmdlet(VerbsCommon.Get, "SecurityIdentifier")]
    public class GetSecurityIdentifier : Cmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the User or Group name to translate ")]
        public string[] UserName { get; set; }

        protected override void ProcessRecord()
        {
            foreach (var sid in UserName.Select(entry => new NTAccount(entry)).Select(account => account.Translate(typeof(SecurityIdentifier))))
            {
                WriteObject(sid);
            }
        }
    }
}