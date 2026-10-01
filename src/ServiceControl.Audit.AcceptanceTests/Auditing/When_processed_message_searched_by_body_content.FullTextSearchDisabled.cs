namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    partial class When_processed_message_searched_by_body_content
    {
        [Test]
        public async Task Should_not_be_found_when_fulltext_search_disabled()
        {
            SetSettings = settings => settings.EnableFullTextSearchOnBodies = false;

            var searchString = "forty-two";

            var context = await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage
                {
                    Something = "Somewhere in the body is the answer to all of the questions. forty-two"
                })))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    if (c.MessageId != null && await this.TryGetMany<MessagesView>($"/api/messages/search/{c.MessageId}"))
                    {
                        c.MessageIngested = true;
                    }

                    if (!c.MessageIngested)
                    {
                        return false;
                    }

                    c.MessageFound = await this.TryGetMany<MessagesView>($"/api/messages/search/{searchString}");
                    return true;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.MessageIngested, Is.True);
                Assert.That(context.MessageFound, Is.False);
            }
        }
    }
}
