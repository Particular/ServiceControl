namespace ServiceControlInstaller.Engine.Accounts
{
    using System;
    using System.DirectoryServices.AccountManagement;
    using System.Linq;
    using System.Security.Principal;
    using Microsoft.Win32;
    using Validation;

    public class UserAccount
    {
        public SecurityIdentifier SID { get; private set; }
        public string Name { get; private set; }
        public string Domain { get; private set; }
        public string DisplayName { get; private set; }

        public string QualifiedName => $@"{Domain}\{Name}";

        public bool IsLocalSystem()
        {
            return SID.IsWellKnown(WellKnownSidType.LocalSystemSid);
        }

        public bool IsLocalService()
        {
            return SID.IsWellKnown(WellKnownSidType.LocalServiceSid);
        }

        // ReSharper disable once InconsistentNaming
        static string LocalizedNTAuthority()
        {
            var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            return localSystem.Translate(typeof(NTAccount)).Value.Split('\\')[0];
        }

        public string RetrieveProfilePath()
        {
            var sidIdentity = SID.Value;
            if (string.IsNullOrWhiteSpace(sidIdentity))
            {
                return null;
            }

            using (var profileListKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList"))
            {
                if (profileListKey != null)
                {
                    using (var profileKey = profileListKey.OpenSubKey(sidIdentity))
                    {
                        if (profileKey != null)
                        {
                            return (string)profileKey.GetValue("ProfileImagePath", null);
                        }
                    }
                }
            }

            return null;
        }

        public bool CheckPassword(string password)
        {
            if (Domain.Equals(LocalizedNTAuthority(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // AD Group Managed Service Account are always named with a trailing $
            // In this case the installing user does not provide a password and Windows handles the password management
            if (Name.EndsWith("$"))
            {
                return true;
            }

            if (password == null)
            {
                throw new EngineValidationException("A password is required for this service account");
            }

            var localAccount = Domain.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
            using (var context = localAccount ? new PrincipalContext(ContextType.Machine) : new PrincipalContext(ContextType.Domain, Domain))
            {
                return context.ValidateCredentials(Name, password, ContextOptions.Negotiate);
            }
        }

        public static UserAccount ParseAccountName(string accountName)
        {
            var systemAliases = new[]
            {
                null,
                "system",
                "local system",
                "localsystem"
            };

            var userAccount = new UserAccount();
            if (systemAliases.Contains(accountName, StringComparer.OrdinalIgnoreCase))
            {
                userAccount.SID = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            }
            else
            {
                var account = accountName.StartsWith(@".\") ? new NTAccount(accountName.Remove(0, 2)) : new NTAccount(accountName);
                userAccount.SID = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
            }

            //Resolve SID back the other way
            var resolvedAccount = (NTAccount)userAccount.SID.Translate(typeof(NTAccount));
            var parts = resolvedAccount.Value.Split("\\".ToCharArray(), 2);
            userAccount.Domain = parts[0];
            userAccount.Name = parts[1];

            if (userAccount.SID.IsWellKnown(WellKnownSidType.LocalSystemSid))
            {
                userAccount.DisplayName = "Local System";
            }
            else
            {
                userAccount.DisplayName = parts[1];
            }

            if (!userAccount.Domain.Equals(LocalizedNTAuthority(), StringComparison.OrdinalIgnoreCase))
            {
                if (!userAccount.SID.IsAccountSid())
                {
                    throw new Exception("Not a valid account");
                }
            }

            return userAccount;
        }
    }
}