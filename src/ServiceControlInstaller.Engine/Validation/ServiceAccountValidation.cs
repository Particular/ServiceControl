namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Linq;
    using Accounts;
    using Api;

    class ServiceAccountValidation
    {
        public static void Validate(IServiceAccount instance)
        {
            var userAccount = UserAccount.ParseAccountName(instance.ServiceAccount);

            if (!userAccount.CheckPassword(instance.ServiceAccountPwd))
            {
                throw new EngineValidationException("Password Invalid");
            }

            try
            {
                SetLogonAsAServicePrivilege(userAccount);
            }
            catch (Exception)
            {
                throw new Exception($"Failed to enable the LogonAsAService privilege on {instance.ServiceAccount}");
            }
        }

        static void SetLogonAsAServicePrivilege(UserAccount userAccount)
        {
            if (!userAccount.Domain.Equals("NT AUTHORITY", StringComparison.OrdinalIgnoreCase))
            {
                var privileges = Lsa.GetPrivileges(userAccount.QualifiedName).ToList();

                if (!privileges.Contains(LogonPrivileges.LogonAsAService, StringComparer.OrdinalIgnoreCase))
                {
                    privileges.Add(LogonPrivileges.LogonAsAService);
                    Lsa.GrantPrivileges(userAccount.QualifiedName, privileges.ToArray());
                }
            }
        }
    }
}