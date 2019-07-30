namespace ServiceControl.UnitTests.Operations
{
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Operations;

    [TestFixture]
    public class FailedErrorImportCustomCheckTests
    {
        [Test]
        public async Task Pass_if_no_failed_imports()
        {
            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                store.ExecuteIndex(new FailedErrorImportIndex());

                var customCheck = new FailedErrorImportCustomCheck(store);

                var result = await customCheck.PerformCheck();

                Assert.AreEqual(CheckResult.Pass, result);
            }
        }

        [Test]
        public async Task Fail_if_failed_imports()
        {
            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                store.ExecuteIndex(new FailedErrorImportIndex());

                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new FailedErrorImport());
                    await session.SaveChangesAsync();
                }

                store.WaitForIndexing();

                var customCheck = new FailedErrorImportCustomCheck(store);

                var result = await customCheck.PerformCheck();

                Assert.IsTrue(result.HasFailed);
                StringAssert.StartsWith("One or more error messages have failed to import properly into ServiceControl and have been stored in the ServiceControl database.", result.FailureReason);
            }
        }
    }
}