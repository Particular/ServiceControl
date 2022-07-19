namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Contracts.Operations;
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
            await fixture.SetupDataStore().ConfigureAwait(false);
        }

        [TearDown]
        public async Task Cleanup()
        {
            await fixture.CleanupDB().ConfigureAwait(false);
        }

        [Test]
        public async Task CustomChecks_load_from_data_store()
        {
            var checkDetails = new CustomCheckDetail
            {
                CustomCheckId = "Test-Check",
                FailureReason = "Testing",
                OriginatingEndpoint = new EndpointDetails
                {
                    Host = "localhost",
                    HostId = Guid.Parse("5F41DEEA-783C-4B88-8558-9371A61F1027"),
                    Name = "test-host"
                }
            };

            var _ = await fixture.CustomCheckDataStore.UpdateCustomCheckStatus(checkDetails).ConfigureAwait(false);

            await fixture.CompleteDBOperation().ConfigureAwait(false);
            var stats = await fixture.CustomCheckDataStore.GetStats(new PagingInfo()).ConfigureAwait(false);

            Assert.AreEqual(1, stats.Results.Count(r => r.CustomCheckId == "Test-Check"));
        }


        readonly PersistenceDataStoreFixture fixture;
    }
}