namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.Entities;

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
        public async Task A_small_text_body_is_kept_inline()
        {
            await Ingest(MakeMessage(), Encoding.UTF8.GetBytes("text"));

            Assert.That(Directory.GetFiles(configuration.BodyStoragePath), Is.Empty);
        }

        [Test]
        public async Task A_text_body_over_the_limit_is_served_in_full_and_stays_searchable()
        {
            var text = "findme " + new string('x', MaxBodySizeToStore);
            await Ingest(MakeMessage(), Encoding.UTF8.GetBytes(text));

            var body = await MessagesViewStore.GetMessageBody(await BodyId(), TestContext.CurrentContext.CancellationToken);
            var found = await MessagesViewStore.QueryMessages("findme", new PagingInfo(), new SortInfo("time_sent", "desc"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Directory.GetFiles(configuration.BodyStoragePath), Has.Length.EqualTo(1));
                Assert.That(await Read(body), Is.EqualTo(Encoding.UTF8.GetBytes(text)));
                Assert.That(body.ContentType, Is.EqualTo("application/json"));
                Assert.That(body.ContentLength, Is.EqualTo(text.Length));
                Assert.That(body.Version.HasValue, Is.True);
                Assert.That(found.Results, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public async Task A_binary_body_is_served_with_its_exact_bytes()
        {
            byte[] bytes = [0, 1, 2, 255, 254];
            await Ingest(MakeMessage(contentType: "application/octet-stream"), bytes);

            var body = await MessagesViewStore.GetMessageBody(await BodyId(), TestContext.CurrentContext.CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(body.Found, Is.True);
                Assert.That(body.HasContent, Is.True);
                Assert.That(body.ContentType, Is.EqualTo("application/octet-stream"));
                Assert.That(body.ContentLength, Is.EqualTo(bytes.Length));
                Assert.That(await Read(body), Is.EqualTo(bytes));
            }
        }

        [Test]
        public async Task A_body_that_is_not_utf8_is_served_with_its_exact_bytes()
        {
            byte[] bytes = [0xC3, 0x28];
            await Ingest(MakeMessage(contentType: null), bytes);

            var body = await MessagesViewStore.GetMessageBody(await BodyId(), TestContext.CurrentContext.CancellationToken);

            Assert.That(await Read(body), Is.EqualTo(bytes));
        }

        [Test]
        public async Task A_text_body_with_a_nul_is_served_with_its_exact_bytes()
        {
            byte[] bytes = "a\0b"u8.ToArray();
            await Ingest(MakeMessage(), bytes);

            var body = await MessagesViewStore.GetMessageBody(await BodyId(), TestContext.CurrentContext.CancellationToken);

            Assert.That(await Read(body), Is.EqualTo(bytes));
        }

        [Test]
        public async Task A_missing_external_body_answers_no_content()
        {
            await Ingest(MakeMessage(contentType: "application/octet-stream"), [1, 2, 3]);
            foreach (var file in Directory.GetFiles(configuration.BodyStoragePath))
            {
                File.Delete(file);
            }

            var body = await MessagesViewStore.GetMessageBody(await BodyId(), TestContext.CurrentContext.CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(body.Found, Is.True);
                Assert.That(body.HasContent, Is.False);
            }
        }

        [Test]
        public async Task A_row_written_before_external_storage_that_was_too_large_answers_no_content()
        {
            await Ingest(MakeMessage(), Encoding.UTF8.GetBytes("text"));
            await SetBodyState(BodyState.TooLarge);

            var body = await MessagesViewStore.GetMessageBody(await BodyId(), TestContext.CurrentContext.CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(body.Found, Is.True);
                Assert.That(body.HasContent, Is.False);
            }
        }

        [Test]
        public async Task A_row_written_before_external_storage_that_was_not_text_is_not_found()
        {
            await Ingest(MakeMessage(), Encoding.UTF8.GetBytes("text"));
            await SetBodyState(BodyState.NotText);

            var body = await MessagesViewStore.GetMessageBody(await BodyId(), TestContext.CurrentContext.CancellationToken);

            Assert.That(body.Found, Is.False);
        }

        Task<int> SetBodyState(BodyState state) =>
            WithDbContext((dbContext, token) => dbContext.AuditMessages.ExecuteUpdateAsync(setters => setters.SetProperty(message => message.BodyState, state), token));

        static async Task<byte[]> Read(MessageBodyView body)
        {
            if (body.StreamContent == null)
            {
                return Encoding.UTF8.GetBytes(body.StringContent);
            }

            await using (body.StreamContent)
            {
                using var buffer = new MemoryStream();
                await body.StreamContent.CopyToAsync(buffer);
                return buffer.ToArray();
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
