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
                services.AddSingleton<TrialLicenseMetadataProvider>();
            };

        [Test]
        public async Task LicenseMetadata_can_be_saved()
        {
            var licenseMetadataService = ServiceProvider.GetRequiredService<TrialLicenseMetadataProvider>();

            await licenseMetadataService.StoreTrialEndDate(
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)), CancellationToken.None);

            var trialStartDate = await licenseMetadataService.GetTrialEndDate(CancellationToken.None);

            Assert.That(trialStartDate, Is.EqualTo(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14))));
        }
    }
}