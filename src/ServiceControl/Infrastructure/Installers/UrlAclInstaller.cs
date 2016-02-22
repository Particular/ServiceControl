namespace ServiceBus.Management.Infrastructure.Installers
{
    using System;
    using System.Security.Principal;
    using NServiceBus;
    using NServiceBus.Installation;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    public class UrlAclInstaller : INeedToInstallSomething
    {
// ReSharper disable once RedundantAssignment
        public void Install(string identity, Configure config)
        {
            // Ignore identity and set URL ACL to localized 'Builtin\Users'
            var accountSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            identity = accountSid.Translate(typeof(NTAccount)).Value;

            if (CurrentUserIsNotAdmin())
            {
                Logger.InfoFormat(@"Did not attempt to grant user '{0}' HttpListener permissions since you are not running with admin privileges", identity);
                return;
            } 
            
            if (Environment.OSVersion.Version.Major <= 5)
            {
                Logger.InfoFormat(
                    @"Did not attempt to grant user '{0}' HttpListener permissions since you are running an old OS. Processing will continue. 
To manually perform this action run the following command for each url from an admin console:
httpcfg set urlacl /u {{http://URL:PORT/[PATH/] | https://URL:PORT/[PATH/]}} /a D:(A;;GX;;;""{0}"")", identity);
                return;
            }

            Logger.InfoFormat("Granting user '{0}' HttpListener permissions to {1}", identity, Settings.ApiUrl);
            var reservation = new UrlReservation(Settings.ApiUrl, accountSid);
            reservation.Create();
        }
        
        static bool CurrentUserIsNotAdmin()
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            return !principal.IsInRole(WindowsBuiltInRole.Administrator);
        }


        static readonly ILog Logger = LogManager.GetLogger(typeof(UrlAclInstaller));
    }
}