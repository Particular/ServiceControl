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
        public async Task Corrects_trial_date_on_disk_in_the_future()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(66)));
            var metadataProvider = new FakeMetadataProvider();
            metadataProvider.Metadata = new TrialMetadata()
            {
                TrialStartDate = DateOnly.FromDateTime(today.AddDays(-10))
            };

            var checkedDetails = await ActiveLicense.EnsureTrialLicenseIsValid(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(checkedDetails.ExpirationDate, Is.EqualTo(today.AddDays(-10).AddDays(14)));
            Assert.That(checkedDetails.HasLicenseExpired(), Is.EqualTo(false));
        }

        [Test]
        public async Task Corrects_trial_date_on_disk_in_the_past()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(-66)));
            var metadataProvider = new FakeMetadataProvider();
            metadataProvider.Metadata = new TrialMetadata()
            {
                TrialStartDate = DateOnly.FromDateTime(today.AddDays(-10))
            };

            var checkedDetails = await ActiveLicense.EnsureTrialLicenseIsValid(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(checkedDetails.ExpirationDate, Is.EqualTo(today.AddDays(-10).AddDays(14)));
            Assert.That(checkedDetails.HasLicenseExpired(), Is.EqualTo(false));
        }

        [Test]
        public async Task Corrects_current_trial_date_on_disk_if_value_in_db_is_in_the_past()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(-7)));
            var metadataProvider = new FakeMetadataProvider();
            metadataProvider.Metadata = new TrialMetadata()
            {
                TrialStartDate = DateOnly.FromDateTime(today.AddDays(-20))
            };

            var checkedDetails = await ActiveLicense.EnsureTrialLicenseIsValid(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(checkedDetails.ExpirationDate, Is.EqualTo(today.AddDays(-20).AddDays(14)));
            Assert.That(checkedDetails.HasLicenseExpired(), Is.EqualTo(true));
        }

        [Test]
        public async Task Detects_tempered_trial_date_in_db()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(14)));
            var metadataProvider = new FakeMetadataProvider();
            metadataProvider.Metadata = new TrialMetadata
            {
                TrialStartDate = DateOnly.FromDateTime(today.AddDays(20))
            };

            var checkedDetails = await ActiveLicense.EnsureTrialLicenseIsValid(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(checkedDetails.ExpirationDate, Is.LessThan(today));
        }

        //[Test]
        //public async Task Detects_trial_date_in_db_in_future()
        //{
        //    var today = DateTime.UtcNow.Date;
        //    var temperedTrial = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(66)));
        //    var metadataProvider = new FakeMetadataProvider();
        //    metadataProvider.Metadata = new TrialMetadata
        //    {
        //        TrialStartDate = DateOnly.FromDateTime(today.AddDays(20))
        //    };

        //    var checkedDetails = await ActiveLicense.EnsureTrialLicenseIsValid(temperedTrial, metadataProvider, CancellationToken.None);

        //    Assert.That(checkedDetails.ExpirationDate, Is.LessThan(today));
        //}

        class FakeMetadataProvider : ILicenseLicenseMetadataProvider
        {
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
