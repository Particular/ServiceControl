namespace ServiceControl.UnitTests.Operations
{
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using Raven.TestDriver;
    using ServiceControl.Operations;

    [TestFixture]
    public class FailedAuditImportCustomCheckTests : RavenTestDriver
    {
        [Test]
        public async Task Pass_if_no_failed_imports()
        {
            using (var store = GetDocumentStore())
            {
                store.ExecuteIndex(new FailedAuditImportIndex());

                var customCheck = new FailedAuditImportCustomCheck(store);

                var result = await customCheck.PerformCheck();

                Assert.AreEqual(CheckResult.Pass, result);
            }
        }

        [Test]
        public async Task Fail_if_failed_imports()
        {
            using (var store = GetDocumentStore())
            {
                store.ExecuteIndex(new FailedAuditImportIndex());

                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new FailedAuditImport());
                    await session.SaveChangesAsync();
                }

                WaitForIndexing(store);

                var customCheck = new FailedAuditImportCustomCheck(store);

                var result = await customCheck.PerformCheck();

                Assert.IsTrue(result.HasFailed);
                StringAssert.StartsWith("One or more audit messages have failed to import properly into ServiceControl and have been stored in the ServiceControl database.", result.FailureReason);
            }
        }
    }
}