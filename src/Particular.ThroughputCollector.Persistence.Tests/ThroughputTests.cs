namespace Particular.ThroughputCollector.Persistence.Tests
{
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class ThroughputTests : PersistenceTestFixture
    {
        public override Task Setup()
        {
            SetSettings = s =>
            {
            };
            return base.Setup();
        }

        //[Test]
        //public async Task Basic_Roundtrip()
        //{
        //    var message = MakeMessage("MyMessageId");

        //    await IngestProcessedMessagesAudits(
        //        message
        //        );

        //    var queryResult = await DataStore.QueryKnownEndpoints();

        //    Assert.That(queryResult.Results.Count, Is.EqualTo(1));
        //    Assert.That(queryResult.Results[0].MessageId, Is.EqualTo("MyMessageId"));
        //}

        //[Test]
        //public async Task Handles_no_results_gracefully()
        //{
        //    var nonExistingMessage = Guid.NewGuid().ToString();
        //    var queryResult = await DataStore.QueryMessages(nonExistingMessage, new PagingInfo(), new SortInfo("Id", "asc"));

        //    Assert.That(queryResult.Results, Is.Empty);
        //}
    }
}