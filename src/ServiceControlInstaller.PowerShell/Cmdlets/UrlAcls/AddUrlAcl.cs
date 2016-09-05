// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnassignedField.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Security.Principal;
    using HttpApiWrapper;

    [Cmdlet(VerbsCommon.Add, "UrlAcl")]
    public class AddUrlAcl : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The URL to add to the URLACL list. This should always in a trailing /")]
        public string Url { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 1, HelpMessage = "The user or group to assign to this URLACL")]
        public string[] Users;

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            var sidList = new List<SecurityIdentifier>();

            foreach (var user in Users)
            {
                try
                {
                    var account = new NTAccount(user);
                    var sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
                    sidList.Add(sid);
                }
                catch(Exception ex)
                {
                    WriteError(new ErrorRecord(ex, "Failed to parse account name", ErrorCategory.InvalidData, user));
                    return;
                }
            }
            UrlReservation.Create(new UrlReservation(Url, sidList.ToArray()));
        }
    }
}