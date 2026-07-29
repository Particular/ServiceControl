namespace ServiceControl.Persistence.Tests.RavenDB.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using ServiceControl.Operations;

    [TestFixture]
    class FailedErrorImportDedupeTests : RavenPersistenceTestBase
    {
        [Test]
        public async Task Repeated_failure_of_the_same_message_stores_one_document()
        {
            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, "message-1" },
                { Headers.ProcessingEndpoint, "Sales" }
            };

            await StoreFailure(headers, "the first failure");
            await StoreFailure(headers, "the second failure");

            DocumentStore.WaitForIndexing();

            using var session = DocumentStore.OpenAsyncSession();
            var documents = await session.Query<FailedErrorImport>().ToListAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(documents, Has.Count.EqualTo(1));
                Assert.That(documents[0].ExceptionInfo, Is.EqualTo("the second failure"));
            }
        }

        Task StoreFailure(IReadOnlyDictionary<string, string> headers, string exceptionInfo) =>
            FailedImportStore.StoreFailedErrorImport(new FailedErrorImport
            {
                Id = FailedErrorImport.DeriveKey(headers, "native-1").ToString(),
                Message = new FailedTransportMessage
                {
                    Id = "native-1",
                    Headers = new Dictionary<string, string>(headers),
                    Body = []
                },
                ExceptionInfo = exceptionInfo
            });
    }
}
