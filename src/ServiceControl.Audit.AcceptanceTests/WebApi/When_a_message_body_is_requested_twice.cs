namespace ServiceControl.Audit.AcceptanceTests.WebApi
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;

    class When_a_message_body_is_requested_twice : AcceptanceTest
    {
        [Test]
        public async Task Should_answer_not_modified()
        {
            string issued = null;
            HttpStatusCode? repeated = null;

            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(bus => bus.SendLocal(new MyMessage { Payload = "PAYLOAD" })))
                .Done(async c =>
                {
                    if (c.MessageId == null)
                    {
                        return false;
                    }

                    MessagesView audited = await this.TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id", m => m.MessageId == c.MessageId);

                    if (audited == null)
                    {
                        return false;
                    }

                    var url = $"/api{audited.BodyUrl}";

                    using var first = await this.GetRaw(url);

                    if (!first.Headers.TryGetValues("ETag", out var values))
                    {
                        return false;
                    }

                    issued = string.Join(string.Empty, values);

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    // Unvalidated, so a malformed validator reaches the server and shows up as a 200.
                    request.Headers.TryAddWithoutValidation("If-None-Match", issued);

                    using var second = await HttpClient.SendAsync(request);
                    repeated = second.StatusCode;

                    return true;
                })
                .Run();

            Assert.That(issued, Is.Not.Null, "the body response carried no validator, so a client can never revalidate it");
            Assert.That(repeated, Is.EqualTo(HttpStatusCode.NotModified), $"the body was sent again to a client that already held {issued}");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithAudit>();

            [Handler]
            public class MyMessageHandler(MyContext testContext, IReadOnlySettings settings) : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    testContext.MessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : ICommand
        {
            public string Payload { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}
