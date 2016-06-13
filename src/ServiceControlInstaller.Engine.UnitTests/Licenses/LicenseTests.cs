namespace ServiceControlInstaller.Engine.UnitTests.Licenses
{
    using System;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.LicenseMgmt;

    [TestFixture]
    public class LicenseTests
    {
        [Test, Explicit]
        public void FindLicenses()
        {
            var license = LicenseManager.FindLicense();

            Console.WriteLine(license.Location);
            if (license.Details != null)
            {
                Console.WriteLine(license.Details.RegisteredTo);
                Console.WriteLine(license.Details.LicenseType);
                if (license.Details.ExpirationDate.HasValue)
                {
                    Console.WriteLine("Exp Date : {0}", license.Details.ExpirationDate.Value);
                }
            }
        }
    }
}
