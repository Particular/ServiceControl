namespace ServiceControl.Audit.Persistence.Tests
{
    using NUnit.Framework;

    [TestFixture]
    class CustomCheckTests : PersistenceTestFixture
    {
        //TODO: Make these test execure the IFailedAuditStorage and not the custom check it self since its in the Sc.Audit project
        //[Test]
        //public async Task Pass_if_no_failed_imports()
        //{
        //    var customCheck = new FailedAuditImportCustomCheck(FailedAuditStorage);

        //    var result = await customCheck.PerformCheck().ConfigureAwait(false);

        //    Assert.AreEqual(CheckResult.Pass, result);
        //}

        //[Test]
        //public async Task Fail_if_failed_imports()
        //{
        //    await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport()).ConfigureAwait(false);

        //    await configuration.CompleteDBOperation().ConfigureAwait(false);

        //    var customCheck = new FailedAuditImportCustomCheck(FailedAuditStorage);

        //    var result = await customCheck.PerformCheck().ConfigureAwait(false);

        //    Assert.IsTrue(result.HasFailed);
        //    StringAssert.StartsWith("One or more audit messages have failed to import properly into ServiceControl.Audit and have been stored in the ServiceControl.Audit database.", result.FailureReason);
        //}
    }
}