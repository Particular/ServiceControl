namespace ServiceControl.UnitTests.Licensing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LicenseManagement;
    using NUnit.Framework;
    using Particular.ServiceControl.Licensing;
    using Persistence;

    [TestFixture]
    public class ActiveLicenseTests
    {
        [Test]
        public async Task Stores_trial_end_date_if_not_found()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(6)));
            var metadataProvider = new FakeMetadataProvider();

            var checkedDetails = await ActiveLicense.ValidateTrialLicense(trialLicense, metadataProvider, CancellationToken.None);
            var trialEndDate = await metadataProvider.GetTrialEndDate(CancellationToken.None);

            Assert.That(trialEndDate, Is.EqualTo(DateOnly.FromDateTime(today.AddDays(6))));

            Assert.That(checkedDetails.ExpirationDate, Is.EqualTo(today.AddDays(6)));
            Assert.That(checkedDetails.HasLicenseExpired(), Is.EqualTo(false));
        }

        [Test]
        public async Task Invalidates_license_if_end_date_in_file_and_db_are_different()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(66)));
            var metadataProvider = new FakeMetadataProvider(new TrialMetadata
            {
                TrialEndDate = DateOnly.FromDateTime(today.AddDays(65))
            });

            var checkedDetails = await ActiveLicense.ValidateTrialLicense(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(checkedDetails.ExpirationDate, Is.LessThan(today));
            Assert.That(checkedDetails.HasLicenseExpired(), Is.True);
        }

        [Test]
        public async Task Accepts_license_if_end_date_in_file_and_db_match()
        {
            var today = DateTime.UtcNow.Date;
            var endDate = DateOnly.FromDateTime(today.AddDays(14));
            var trialLicense = LicenseDetails.TrialFromEndDate(endDate);
            var metadataProvider = new FakeMetadataProvider(new TrialMetadata
            {
                TrialEndDate = endDate
            });

            var checkedDetails = await ActiveLicense.ValidateTrialLicense(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(checkedDetails.ExpirationDate, Is.GreaterThanOrEqualTo(today));
            Assert.That(checkedDetails.HasLicenseExpired, Is.False);
        }

        class FakeMetadataProvider : ITrialLicenseMetadataProvider
        {
            TrialMetadata metadata;

            public FakeMetadataProvider() : this(null)
            {
            }

            public FakeMetadataProvider(TrialMetadata metadata) => this.metadata = metadata;

            public Task<DateOnly?> GetTrialEndDate(CancellationToken cancellationToken) => Task.FromResult(metadata?.TrialEndDate);

            public Task StoreTrialEndDate(DateOnly trialEndDate, CancellationToken cancellationToken)
            {
                metadata ??= new TrialMetadata();
                metadata.TrialEndDate = trialEndDate;
                return Task.CompletedTask;
            }
        }
    }
}
