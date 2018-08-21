namespace ServiceControlInstaller.Engine.UnitTests.Account
{
    using System;
    using System.DirectoryServices.AccountManagement;
    using System.Linq;
    using System.Security.Principal;
    using Accounts;
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
    }
}