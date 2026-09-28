namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    class EFBodyTests : EFPersistenceTestFixture
    {
        public override Task Setup()
        {
            SetSettings = settings => settings.MaxBodySizeToStore = MaxBodySizeToStore;
            return base.Setup();
        }

        [Test]
        public async Task Serves_a_text_body()
        {
            var message = MakeMessage();
            await Ingest(message, Encoding.UTF8.GetBytes("""{"OrderId":"42"}"""));

            var body = await MessagesViewStore.GetMessageBody(await BodyId(), TestContext.CurrentContext.CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(body.HasContent, Is.True);
                Assert.That(body.StringContent, Is.EqualTo("""{"OrderId":"42"}"""));
                Assert.That(body.ContentType, Is.EqualTo("application/json"));
                Assert.That(body.Version.HasValue, Is.True);
            }
        }

        [Test]
        public async Task Finds_a_body_by_the_unique_message_id_alone()
        {
            var message = MakeMessage();
            await Ingest(message, Encoding.UTF8.GetBytes("text"));

            var body = await MessagesViewStore.GetMessageBody(message.UniqueMessageId, TestContext.CurrentContext.CancellationToken);

            Assert.That(body.StringContent, Is.EqualTo("text"));
        }

        [Test]
        public async Task A_text_body_over_the_limit_is_not_served_but_stays_searchable()
        {
            var text = "findme " + new string('x', MaxBodySizeToStore);
            await Ingest(MakeMessage(), Encoding.UTF8.GetBytes(text));

            var body = await MessagesViewStore.GetMessageBody(await BodyId(), TestContext.CurrentContext.CancellationToken);
            var found = await MessagesViewStore.QueryMessages("findme", new PagingInfo(), new SortInfo("time_sent", "desc"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(body.Found, Is.True);
                Assert.That(body.HasContent, Is.False, "the body is over MaxBodySizeToStore, which answers 204");
                Assert.That(found.Results, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public async Task A_binary_body_is_not_stored()
        {
            await Ingest(MakeMessage(contentType: "application/octet-stream"), [1, 2, 3]);

            var body = await MessagesViewStore.GetMessageBody(await BodyId(), TestContext.CurrentContext.CancellationToken);

            Assert.That(body.Found, Is.False);
        }

        [Test]
        public async Task A_body_that_is_not_utf8_is_not_stored()
        {
            await Ingest(MakeMessage(contentType: null), [0xC3, 0x28]);

            var body = await MessagesViewStore.GetMessageBody(await BodyId(), TestContext.CurrentContext.CancellationToken);

            Assert.That(body.Found, Is.False);
        }

        [Test]
        public async Task A_binary_body_over_the_limit_answers_that_it_was_too_large()
        {
            await Ingest(MakeMessage(contentType: "application/octet-stream"), new byte[MaxBodySizeToStore + 1]);

            var body = await MessagesViewStore.GetMessageBody(await BodyId(), TestContext.CurrentContext.CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(body.Found, Is.True);
                Assert.That(body.HasContent, Is.False);
            }
        }

        [Test]
        public async Task A_message_without_a_body_has_no_body_url()
        {
            await Ingest(MakeMessage());

            var view = (await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"))).Results.Single();

            Assert.That(view.BodyUrl, Is.Null);
        }

        [Test]
        public async Task An_unknown_body_is_not_found()
        {
            var body = await MessagesViewStore.GetMessageBody($"2026010100-{Guid.NewGuid()}", TestContext.CurrentContext.CancellationToken);

            Assert.That(body.Found, Is.False);
        }

        async Task<string> BodyId()
        {
            var bodyUrl = (await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"))).Results.Single().BodyUrl;
            return bodyUrl.Split('/')[2];
        }

        const int MaxBodySizeToStore = 1000;
    }
}
