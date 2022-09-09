namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Audit.Auditing;

    [TestFixture]
    class CustomCheckTests : PersistenceTestFixture
    {
        [Test]
        public async Task Pass_if_no_failed_imports()
        {
            var customCheck = new FailedAuditImportCustomCheck(DataStore);

            var result = await customCheck.PerformCheck().ConfigureAwait(false);

            Assert.AreEqual(CheckResult.Pass, result);
        }

        [Test]
        public async Task Fail_if_failed_imports()
        {
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport()).ConfigureAwait(false);

            await configuration.CompleteDBOperation().ConfigureAwait(false);

            var customCheck = new FailedAuditImportCustomCheck(DataStore);

            var result = await customCheck.PerformCheck().ConfigureAwait(false);

            Assert.IsTrue(result.HasFailed);
            StringAssert.StartsWith("One or more audit messages have failed to import properly into ServiceControl.Audit and have been stored in the ServiceControl.Audit database.", result.FailureReason);
        }
    }
}