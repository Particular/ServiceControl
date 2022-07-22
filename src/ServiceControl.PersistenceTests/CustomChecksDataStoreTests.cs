namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Contracts.Operations;
    using CustomChecks;
    using Infrastructure;
    using NUnit.Framework;

    [TestFixtureSource(typeof(PersistenceTestCollection))]
    class CustomChecksDataStoreTests
    {
        public CustomChecksDataStoreTests(PersistenceDataStoreFixture fixture)
        {
            this.fixture = fixture;
        }

        [SetUp]
        public async Task Setup()
        {
            await fixture.CleanupDB().ConfigureAwait(false);
            await fixture.SetupDataStore().ConfigureAwait(false);
        }

        [Test]
        public async Task CustomChecks_load_from_data_store()
        {
            var checkDetails = new CustomCheckDetail
            {
                Category = "test-category",
                CustomCheckId = "Test-Check",
                HasFailed = true,
                FailureReason = "Testing",
                OriginatingEndpoint = new EndpointDetails
                {
                    Host = "localhost",
                    HostId = Guid.Parse("5F41DEEA-783C-4B88-8558-9371A61F1027"),
                    Name = "test-host"
                },
            };

            var status = await fixture.CustomCheckDataStore.UpdateCustomCheckStatus(checkDetails).ConfigureAwait(false);

            await fixture.CompleteDBOperation().ConfigureAwait(false);
            var stats = await fixture.CustomCheckDataStore.GetStats(new PagingInfo()).ConfigureAwait(false);

            Assert.AreEqual(1, stats.Results.Count);
            Assert.AreEqual(Status.Fail, stats.Results[0].Status);
            Assert.AreEqual(CheckStateChange.Changed, status);
        }

        [Test]
        public async Task Storing_failed_custom_checks_returns_unchanged()
        {
            var checkDetails = new CustomCheckDetail
            {
                Category = "test-category",
                CustomCheckId = "Test-Check",
                HasFailed = true,
                FailureReason = "Testing",
                OriginatingEndpoint = new EndpointDetails
                {
                    Host = "localhost",
                    HostId = Guid.Parse("5F41DEEA-783C-4B88-8558-9371A61F1027"),
                    Name = "test-host"
                },
            };

            var statusInitial = await fixture.CustomCheckDataStore.UpdateCustomCheckStatus(checkDetails).ConfigureAwait(false);
            var statusUpdate = await fixture.CustomCheckDataStore.UpdateCustomCheckStatus(checkDetails).ConfigureAwait(false);

            await fixture.CompleteDBOperation().ConfigureAwait(false);

            Assert.AreEqual(CheckStateChange.Changed, statusInitial);
            Assert.AreEqual(CheckStateChange.Unchanged, statusUpdate);
        }

        [Test]
        public async Task Retrieving_custom_checks_by_status()
        {
            var checkDetails = new CustomCheckDetail
            {
                Category = "test-category",
                CustomCheckId = "Test-Check",
                HasFailed = false,
                FailureReason = "Testing",
                OriginatingEndpoint = new EndpointDetails
                {
                    Host = "localhost",
                    HostId = Guid.Parse("5F41DEEA-783C-4B88-8558-9371A61F1027"),
                    Name = "test-host"
                },
            };

            var _ = await fixture.CustomCheckDataStore.UpdateCustomCheckStatus(checkDetails).ConfigureAwait(false);

            await fixture.CompleteDBOperation().ConfigureAwait(false);

            var stats = await fixture.CustomCheckDataStore.GetStats(new PagingInfo(), "pass").ConfigureAwait(false);

            Assert.AreEqual(1, stats.Results.Count);
        }

        readonly PersistenceDataStoreFixture fixture;
    }
}