namespace ServiceControlInstaller.Engine.UnitTests.Account
{
    using System;
    using System.Collections.Generic;
    using Accounts;
    using NUnit.Framework;

    [TestFixture]
    public class UserAccountParsingTest
    {
        [Test]
        public void ParseAccountNamesTest()
        {
            var accountNames = new List<string>
            {
                @"NT Authority\system",
                "local system",
                "system",
                "localsystem",
                Environment.UserName,
                "networkservice"
            };

            foreach (var accountName in accountNames)
            {
                Assert.DoesNotThrow(() => UserAccount.ParseAccountName(accountName));
            }
        }
    }
}