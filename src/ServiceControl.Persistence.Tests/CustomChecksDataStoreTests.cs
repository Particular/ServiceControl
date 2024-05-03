namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    class CustomChecksDataStoreTests : PersistenceTestBase
    {
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
                    HostId = Guid.Parse("55D0800D-CC90-47C3-83EB-DDE292140C28"),
                    Name = "test-host"
                },
            };

            var status = await CustomChecks.UpdateCustomCheckStatus(checkDetails);

            CompleteDatabaseOperation();
            var stats = await CustomChecks.GetStats(new PagingInfo());

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

            var statusInitial = await CustomChecks.UpdateCustomCheckStatus(checkDetails);
            var statusUpdate = await CustomChecks.UpdateCustomCheckStatus(checkDetails);

            CompleteDatabaseOperation();

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
                    HostId = Guid.Parse("C4BDF251-5D22-4A29-AB47-7F1F3226E614"),
                    Name = "test-host"
                },
            };

            var _ = await CustomChecks.UpdateCustomCheckStatus(checkDetails);

            CompleteDatabaseOperation();

            var stats = await CustomChecks.GetStats(new PagingInfo(), "pass");

            Assert.AreEqual(1, stats.Results.Count);
        }

        [Test]
        public async Task Should_delete_custom_checks()
        {
            var checkDetails = new CustomCheckDetail
            {
                Category = "test-category",
                CustomCheckId = "deletion-test-check",
                HasFailed = false,
                OriginatingEndpoint = new EndpointDetails
                {
                    Host = "localhost",
                    HostId = Guid.Parse("80996916-9B8D-4DE1-864A-4EDD359CC98F"),
                    Name = "test-host"
                },
            };

            var checkId = checkDetails.GetDeterministicId();

            var _ = await CustomChecks.UpdateCustomCheckStatus(checkDetails);

            CompleteDatabaseOperation();

            await CustomChecks.DeleteCustomCheck(checkId);

            CompleteDatabaseOperation();

            var storedChecks = await CustomChecks.GetStats(new PagingInfo());
            var check = storedChecks.Results.Where(c => c.Id == checkId.ToString()).ToList();

            Assert.AreEqual(0, check.Count);
        }
    }
}