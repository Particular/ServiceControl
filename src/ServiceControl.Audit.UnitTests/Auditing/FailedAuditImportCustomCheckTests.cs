namespace ServiceControl.Audit.UnitTests.Auditing
{
    using System.Threading.Tasks;
    using Audit.Auditing;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using Raven.TestDriver;

    [TestFixture]
    public class FailedAuditImportCustomCheckTests : RavenTestDriver
    {
        [Test]
        public async Task Pass_if_no_failed_imports()
        {
            using (var store = GetDocumentStore())
            {
                await store.ExecuteIndexAsync(new FailedAuditImportIndex());

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
                await store.ExecuteIndexAsync(new FailedAuditImportIndex());

                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new FailedAuditImport());
                    await session.SaveChangesAsync();
                }

                WaitForIndexing(store);

                var customCheck = new FailedAuditImportCustomCheck(store);

                var result = await customCheck.PerformCheck();

                Assert.IsTrue(result.HasFailed);
                StringAssert.StartsWith("One or more audit messages have failed to import properly into ServiceControl.Audit and have been stored in the ServiceControl.Audit database.", result.FailureReason);
            }
        }
    }
}