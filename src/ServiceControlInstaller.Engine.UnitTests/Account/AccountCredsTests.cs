namespace ServiceControlInstaller.Engine.UnitTests.Account
{
    using System;
    using System.DirectoryServices.AccountManagement;
    using System.Linq;
    using System.Security.Principal;
    using System.ServiceProcess;
    using Accounts;
    using Engine.Validation;
    using NUnit.Framework;

    [TestFixture]
    public class AccountCredsTests
    {
        [Test]
        public void TestValidLoginChecker()
        {
            var context = new PrincipalContext(ContextType.Machine);
            var adminGroup = GroupPrincipal.FindByIdentity(context, IdentityType.Name, "Administrators");

            // User the local administrator account and check the guessed password fails.
            var adminAccount = adminGroup?.Members.FirstOrDefault(p => (p.Context.ContextType == ContextType.Machine) && p.Sid.IsWellKnown(WellKnownSidType.AccountAdministratorSid));
            if (adminAccount != null)
            {
                Assert.IsFalse(UserAccount.ParseAccountName(adminAccount.Name).CheckPassword($"XXX{Guid.NewGuid():B}"), "Test for Local Admin account should have been false");
            }

            Assert.IsTrue(UserAccount.ParseAccountName("System").CheckPassword(null), "Test for LocalSystem should have been true");
            Assert.Throws<IdentityNotMappedException>(() => UserAccount.ParseAccountName(@"NT Authority\NotAValidAccount").CheckPassword(null), "Test for Invalid System Account should throw IdentityNotMappedException");
            Assert.Throws<IdentityNotMappedException>(() => UserAccount.ParseAccountName("missingaccount").CheckPassword("foo"), "Test for Missing Account should should throw IdentityNotMappedException");
            Assert.Throws<IdentityNotMappedException>(() => UserAccount.ParseAccountName(@"UnknownDomain\AUser").CheckPassword("foo"), "Test for unknown domain should throw IdentityNotMappedException");
        }

        [Test]
        public void TestIfServiceAccountAreSupportedByParser()
        {
            var serviceName = "EventLog";

            var ctl = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == serviceName);
            if (ctl == null)
            {
                Assert.Inconclusive($"{serviceName} service not present for testing");
            }

            // Using EventLog as that is a service available on all windows environments
            var accountName = @"NT SERVICE\" + serviceName;

            var account = UserAccount.ParseAccountName(accountName);

            Assert.IsTrue(account.CheckPassword(""), "Service account passwords must be blank.");
            Assert.Throws<EngineValidationException>(() => account.CheckPassword("MySecret!"), "Service account password cannot have a value and must be blank");
        }
    }
}
