namespace ServiceControl.UnitTests.Operations
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControl.Operations;

    [TestFixture]
    public class UpdateLicenseEnricherTest
    {
        [Test]
        [TestCase(true, "expired")]
        [TestCase(false, "valid")]
        public void Status_Should_Be_Set_When_Header_Contains_License_Expiration(bool hasLicenseExpired, string expectedResult)
        {
            var headers = new Dictionary<string, string>();
            const string hasLicenseExpiredKey = "$.diagnostics.license.expired";
            headers.Add(hasLicenseExpiredKey, hasLicenseExpired.ToString());
            var licenseEnricher = new UpdateLicenseEnricher();
            var status = licenseEnricher.GetLicenseStatus(headers);
            Assert.IsTrue(status.Equals(expectedResult));
        }

        [Test]
        public void Status_Should_Be_Empty_When_Header_Does_Not_Contain_License_Expiration()
        {
            var headers = new Dictionary<string, string>();
            var licenseEnricher = new UpdateLicenseEnricher();
            var status = licenseEnricher.GetLicenseStatus(headers);
            Assert.IsTrue(string.IsNullOrEmpty(status));
        }
    }
}
