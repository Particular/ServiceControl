namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    partial class When_messages_with_big_bodies_are_ingested
    {
        [Test]
        public async Task Should_serve_the_full_body_when_it_is_larger_than_MaxBodySizeToStore()
        {
            SetSettings = settings => settings.MaxBodySizeToStore = MAX_BODY_SIZE;

            var expected = new byte[MAX_BODY_SIZE + 1000];
            byte[] body = null;

            await Define<Context>()
                .WithEndpoint<FatMessageEndpoint>(c => c.When(b => b.SendLocal(new BigFatMessage { BigFatBody = expected })))
                .Done(
                    async c =>
                    {
                        if (c.MessageId == null)
                        {
                            return false;
                        }

                        var result = await this.TryGetSingle<MessagesView>("/api/messages", r => r.MessageId == c.MessageId);
                        MessagesView auditMessage = result;
                        if (!result)
                        {
                            return false;
                        }

                        body = await this.DownloadData($"/api{auditMessage.BodyUrl}");

                        return true;
                    })
                .Run();

            using var document = JsonDocument.Parse(body);
            Assert.That(document.RootElement.GetProperty(nameof(BigFatMessage.BigFatBody)).GetBytesFromBase64(), Is.EqualTo(expected));
        }
    }
}
