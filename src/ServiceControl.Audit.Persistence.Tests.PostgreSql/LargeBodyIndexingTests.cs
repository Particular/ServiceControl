namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    class LargeBodyIndexingTests : EFPersistenceTestFixture
    {
        public override Task Setup()
        {
            SetSettings = settings => settings.MaxBodySizeToStore = 2 * 1024 * 1024;
            return base.Setup();
        }

        [Test]
        public async Task Stores_and_searches_a_large_body_of_distinct_tokens()
        {
            var tokens = Enumerable.Range(0, 31_000).Select(_ => Guid.NewGuid().ToString("N")).ToArray();
            await Ingest(MakeMessage(), Encoding.UTF8.GetBytes(string.Join(" ", tokens)));

            var found = await MessagesViewStore.QueryMessages(tokens[0], new PagingInfo(), new SortInfo("time_sent", "desc"));

            Assert.That(found.Results, Has.Count.EqualTo(1));
        }
    }
}
