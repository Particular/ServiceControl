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
        public async Task Stores_trial_start_date_if_not_found()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(6)));
            var metadataProvider = new FakeMetadataProvider();

            var checkedDetails = await ActiveLicense.EnsureTrialLicenseIsValid(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(metadataProvider.Metadata, Is.Not.Null);
            Assert.That(metadataProvider.Metadata.TrialStartDate, Is.Not.EqualTo(DateOnly.FromDateTime(today.AddDays(6))));

            Assert.That(checkedDetails.ExpirationDate, Is.EqualTo(today.AddDays(6)));
            Assert.That(checkedDetails.HasLicenseExpired(), Is.EqualTo(false));
        }

        [Test]
        public async Task Overrides_future_disk_trial_start_date_with_db_value_non_expired()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(66)));
            var metadataProvider = new FakeMetadataProvider(new TrialMetadata
            {
                TrialStartDate = DateOnly.FromDateTime(today.AddDays(-10))
            });

            var checkedDetails = await ActiveLicense.EnsureTrialLicenseIsValid(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(checkedDetails.ExpirationDate, Is.EqualTo(today.AddDays(-10).AddDays(14)));
            Assert.That(checkedDetails.HasLicenseExpired(), Is.False);
        }

        [Test]
        public async Task Overrides_past_disk_trial_start_date_with_db_value_non_expired()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(-66)));
            var metadataProvider = new FakeMetadataProvider(new TrialMetadata
            {
                TrialStartDate = DateOnly.FromDateTime(today.AddDays(-10))
            });

            var checkedDetails = await ActiveLicense.EnsureTrialLicenseIsValid(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(checkedDetails.ExpirationDate, Is.EqualTo(today.AddDays(-10).AddDays(14)));
            Assert.That(checkedDetails.HasLicenseExpired(), Is.False);
        }

        [Test]
        public async Task Overrides_disk_trial_start_date_with_db_value_expired()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(-7)));
            var metadataProvider = new FakeMetadataProvider(new TrialMetadata
            {
                TrialStartDate = DateOnly.FromDateTime(today.AddDays(-20))
            });

            var checkedDetails = await ActiveLicense.EnsureTrialLicenseIsValid(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(checkedDetails.ExpirationDate, Is.EqualTo(today.AddDays(-20).AddDays(14)));
            Assert.That(checkedDetails.HasLicenseExpired(), Is.True);
        }

        [Test]
        public async Task Detects_tempered_trial_date_in_db_and_voids_license()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(14)));
            var metadataProvider = new FakeMetadataProvider(new TrialMetadata
            {
                TrialStartDate = DateOnly.FromDateTime(today.AddDays(20))
            });

            var checkedDetails = await ActiveLicense.EnsureTrialLicenseIsValid(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(checkedDetails.ExpirationDate, Is.LessThan(today));
            Assert.That(checkedDetails.HasLicenseExpired(), Is.True);
        }

        class FakeMetadataProvider : ILicenseLicenseMetadataProvider
        {
            public FakeMetadataProvider()
            {
            }

            public FakeMetadataProvider(TrialMetadata metadata)
            {
                Metadata = metadata;
            }

            public TrialMetadata Metadata { get; set; }

            public Task<TrialMetadata> GetLicenseMetadata(CancellationToken cancellationToken)
            {
                return Task.FromResult(Metadata);
            }

            public Task InsertLicenseMetadata(TrialMetadata licenseMetadata, CancellationToken cancellationToken)
            {
                Metadata = licenseMetadata;
                return Task.CompletedTask;
            }
        }
    }
}
