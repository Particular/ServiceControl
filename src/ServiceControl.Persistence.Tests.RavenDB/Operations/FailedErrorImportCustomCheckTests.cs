namespace ServiceControl.Persistence.Tests.RavenDB.Operations
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Operations;

    [TestFixture]
    class FailedErrorImportCustomCheckTests : RavenPersistenceTestBase
    {
        public FailedErrorImportCustomCheckTests() =>
            RegisterServices = services =>
            {
                services.AddSingleton<FailedErrorImportCustomCheck>();
            };

        [Test]
        public async Task Pass_if_no_failed_imports()
        {
            await DocumentStore.ExecuteIndexAsync(new FailedErrorImportIndex());

            var customCheck = ServiceProvider.GetRequiredService<FailedErrorImportCustomCheck>();

            var result = await customCheck.PerformCheck();

            Assert.That(result, Is.EqualTo(CheckResult.Pass));
        }

        [Test]
        public async Task Fail_if_failed_imports()
        {
            await DocumentStore.ExecuteIndexAsync(new FailedErrorImportIndex());

            using (var session = DocumentStore.OpenAsyncSession())
            {
                await session.StoreAsync(new FailedErrorImport
                {
                    Id = FailedErrorImport.MakeDocumentId(Guid.NewGuid())
                });

                BlockToInspectDatabase();
                await session.SaveChangesAsync();
            }

            DocumentStore.WaitForIndexing();

            var customCheck = ServiceProvider.GetRequiredService<FailedErrorImportCustomCheck>();

            var result = await customCheck.PerformCheck();

            Assert.Multiple(() =>
            {
                Assert.That(result.HasFailed, Is.True);
                Assert.That(result.FailureReason, Does.StartWith("One or more error messages have failed to import properly into ServiceControl and have been stored in the ServiceControl database."));
            });
        }
    }
}