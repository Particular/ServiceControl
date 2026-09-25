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

    class When_a_list_endpoint_is_revalidated : AcceptanceTest
    {
        [Test]
        public async Task Should_answer_not_modified_for_a_raven_list_endpoint()
        {
            Answer issued = null;
            Answer repeated = null;

            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(bus => bus.SendLocal(new MyMessage { Payload = "PAYLOAD" })))
                .Done(async c =>
                {
                    if (c.MessageId == null)
                    {
                        return false;
                    }

                    // Wait for the message to appear
                    MessagesView audited = await this.TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id", m => m.MessageId == c.MessageId);

                    if (audited == null)
                    {
                        return false;
                    }

                    issued = await Ask("GET", "/api/messages?include_system_messages=false&sort=id", ifNoneMatch: null);

                    if (issued.Etag == null)
                    {
                        return false;
                    }

                    repeated = await Ask("GET", "/api/messages?include_system_messages=false&sort=id", issued.Etag);

                    return true;
                })
                .Run();

            Assert.That(issued.Etag, Is.Not.Null, "the list response carried no ETag, so there is nothing for a client to revalidate against");
            Assert.That(repeated.Status, Is.EqualTo(HttpStatusCode.NotModified), $"the list was sent again to a client that already held {issued.Etag}");
            Assert.That(repeated.TotalCount, Is.Not.Null.And.EqualTo(issued.TotalCount), "the 304 did not carry its Total-Count through");
        }

        [Test]
        public async Task Should_answer_not_modified_for_an_empty_raven_list_endpoint()
        {
            Answer issued = null;
            Answer repeated = null;

            await Define<MyContext>()
                .Done(async c =>
                {
                    issued = await Ask("GET", "/api/conversations/no-such-conversation", ifNoneMatch: null);

                    if (issued.Etag == null)
                    {
                        return false;
                    }

                    repeated = await Ask("GET", "/api/conversations/no-such-conversation", issued.Etag);

                    return true;
                })
                .Run();

            Assert.That(issued.Etag, Is.Not.Null, "an empty result should still carry the Raven query token as its ETag");
            Assert.That(repeated.Status, Is.EqualTo(HttpStatusCode.NotModified), $"the empty list was sent again to a client that already held {issued.Etag}");
        }

        [Test]
        public async Task Audit_counts_should_emit_no_etag()
        {
            Answer answer = null;

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

                    answer = await Ask("GET", $"/api/endpoints/{c.EndpointNameOfReceivingEndpoint}/audit-count", ifNoneMatch: null);

                    return true;
                })
                .Run();

            Assert.That(answer.Etag, Is.Null, "audit counts were not previously versioned and should not gain an ETag");
        }

        async Task<Answer> Ask(string method, string url, string ifNoneMatch)
        {
            using var response = await Send(method, url, ifNoneMatch);

            return new Answer(
                response.StatusCode,
                Header(response, "ETag"),
                Header(response, "Total-Count"));
        }

        static string Header(HttpResponseMessage response, string name) =>
            response.Headers.TryGetValues(name, out var values) ? string.Join(string.Empty, values) : null;

        Task<HttpResponseMessage> Send(string method, string url, string ifNoneMatch)
        {
            var request = new HttpRequestMessage(new HttpMethod(method), url);

            if (ifNoneMatch != null)
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
            }

            return HttpClient.SendAsync(request);
        }

        record Answer(HttpStatusCode Status, string Etag, string TotalCount);

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