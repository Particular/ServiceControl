namespace ServiceControl.UnitTests.Licensing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LicenseManagement;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;
    using Particular.ServiceControl.Licensing;
    using Persistence;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Connector.MassTransit;
    using ServiceControl.Licensing;
    using ServiceControl.Monitoring.HeartbeatMonitoring;

    [TestFixture]
    public class ActiveLicenseTests
    {
        [Test]
        public async Task Stores_trial_end_date_if_not_found()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.FromDateTime(today.AddDays(6)));
            var metadataProvider = new FakeDataProvider();

            var checkedDetails = await ActiveLicense.ValidateTrialLicense(trialLicense, metadataProvider, CancellationToken.None);
            var trialEndDate = await metadataProvider.GetTrialEndDate(CancellationToken.None);

            Assert.That(trialEndDate, Is.EqualTo(DateOnly.FromDateTime(today.AddDays(6))));

            Assert.That(checkedDetails.ExpirationDate, Is.EqualTo(today.AddDays(6)));
            Assert.That(checkedDetails.HasLicenseExpired(), Is.EqualTo(false));
        }

        [Test]
        public async Task Invalidates_license_if_end_date_in_db_more_than_14_days()
        {
            var today = DateTime.UtcNow.Date;
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.MinValue);
            var metadataProvider = new FakeDataProvider(new TrialMetadata
            {
                TrialEndDate = DateOnly.FromDateTime(today.AddDays(15))
            });

            var checkedDetails = await ActiveLicense.ValidateTrialLicense(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(checkedDetails.ExpirationDate, Is.LessThan(today));
            Assert.That(checkedDetails.HasLicenseExpired(), Is.True);
        }

        [Test]
        public async Task Accepts_license_base_on_the_db_value_only()
        {
            var today = DateTime.UtcNow.Date;
            var endDate = DateOnly.FromDateTime(today.AddDays(14));
            var trialLicense = LicenseDetails.TrialFromEndDate(DateOnly.MinValue);
            var metadataProvider = new FakeDataProvider(new TrialMetadata
            {
                TrialEndDate = endDate
            });

            var checkedDetails = await ActiveLicense.ValidateTrialLicense(trialLicense, metadataProvider, CancellationToken.None);

            Assert.That(checkedDetails.ExpirationDate, Is.GreaterThanOrEqualTo(today));
            Assert.That(checkedDetails.HasLicenseExpired, Is.False);
        }

        [TestCase(false, true, "https://particular.net/extend-your-trial?p=servicepulse")]
        [TestCase(true, true, "https://particular.net/license/mt?p=servicepulse&t=0")]
        [TestCase(true, false, "https://particular.net/license/mt?p=servicepulse&t=1")]
        public async Task License_information_preserves_the_existing_mapping_and_renewal_links(bool massTransit, bool evaluation, string expectedUrl)
        {
            var details = LicenseDetails.TrialFromEndDate(new DateOnly(2026, 9, 30));
            var active = new ActiveLicense(null, NullLogger<ActiveLicense>.Instance)
            {
                Details = details,
                IsValid = false,
                IsEvaluation = evaluation
            };
            var connector = new MassTransitConnectorHeartbeatStatus();
            if (massTransit)
            {
                connector.Update(new MassTransitConnectorHeartbeat
                {
                    Version = "1.0.0",
                    ErrorQueues = [],
                    Logs = [],
                    SentDateTimeOffset = DateTimeOffset.MinValue
                });
            }

            var provider = new LicenseInfoProvider(active, new Settings { InstanceName = "Primary" }, connector);
            var result = await provider.GetLicense(false, "servicepulse");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.LicenseExtensionUrl, Is.EqualTo(expectedUrl));
                Assert.That(result.LicenseStatus, Is.EqualTo(details.Status));
                Assert.That(result.Status, Is.EqualTo("invalid"));
                Assert.That(result.TrialLicense, Is.True);
                Assert.That(result.LicenseType, Is.EqualTo(details.LicenseType));
                Assert.That(result.ExpirationDate, Is.EqualTo(details.ExpirationDate?.ToString("O")));
                Assert.That(result.UpgradeProtectionExpiration, Is.Empty);
                Assert.That(result.InstanceName, Is.EqualTo("Primary"));
            }
        }

        class FakeDataProvider : ITrialLicenseDataProvider
        {
            TrialMetadata metadata;

            public FakeDataProvider() : this(null)
            {
            }

            public FakeDataProvider(TrialMetadata metadata) => this.metadata = metadata;

            public Task<DateOnly?> GetTrialEndDate(CancellationToken cancellationToken = default) => Task.FromResult(metadata?.TrialEndDate);

            public Task StoreTrialEndDate(DateOnly trialEndDate, CancellationToken cancellationToken = default)
            {
                metadata ??= new TrialMetadata();
                metadata.TrialEndDate = trialEndDate;
                return Task.CompletedTask;
            }
        }
    }
}
