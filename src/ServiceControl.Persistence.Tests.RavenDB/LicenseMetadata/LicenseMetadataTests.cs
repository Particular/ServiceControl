namespace ServiceControl.Persistence.Tests.RavenDB.LicenseMetadata
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using ServiceControl.Persistence.RavenDB;

    [TestFixture]
    class LicenseMetadataTests : RavenPersistenceTestBase
    {
        public LicenseMetadataTests() =>
            RegisterServices = services =>
            {
                services.AddSingleton<LicenseLicenseMetadataProvider>();
            };

        [Test]
        public async Task LicenseMetadata_can_be_saved()
        {
            var licenseMetadataService = ServiceProvider.GetRequiredService<LicenseLicenseMetadataProvider>();

            var metaData = new TrialMetadata
            {
                TrialStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14))
            };

            await licenseMetadataService.InsertLicenseMetadata(metaData, CancellationToken.None);

            var result = await licenseMetadataService.GetLicenseMetadata(CancellationToken.None);

            Assert.That(result.TrialStartDate, Is.EqualTo(metaData.TrialStartDate));
        }
    }
}