namespace ServiceBus.Management.AcceptanceTests.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Settings;

    class When_requesting_a_message_body : AcceptanceTest
    {
        [Test]
        public async Task Should_be_forwarded_to_audit_instance()
        {
            CustomServiceControlAuditSettings = s => addressOfAuditInstance = s.ApiUrl;

            HttpResponseMessage response = null;
            MessagesView capturedMessage = null;

            var context = await Define<MyContext>()
                .WithEndpoint<RemoteEndpoint>(b => b.When(async (bus, ctx) =>
                {
                    ctx.AuditInstanceId = InstanceIdGenerator.FromApiUrl(addressOfAuditInstance);
                    await bus.SendLocal(new MyMessage());
                }))
                .Done(async c =>
                {
                    if (string.IsNullOrWhiteSpace(c.AuditInstanceMessageId))
                    {
                        return false;
                    }

                    if (!c.MessageAudited)
                    {
                        var result = await this.TryGetMany<MessagesView>("/api/messages", msg => msg.MessageId == c.AuditInstanceMessageId, ServiceControlInstanceName);
                        List<MessagesView> messages = result;
                        if (!result)
                        {
                            return false;
                        }

                        c.MessageAudited = true;
                        capturedMessage = messages.Single(msg => msg.MessageId == c.AuditInstanceMessageId);
                    }

                    response = await this.GetRaw($"/api/{capturedMessage.BodyUrl}", ServiceControlInstanceName);
                    Console.WriteLine($"GetRaw for {c.AuditInstanceMessageId} resulted in {response.StatusCode}");
                    return response.StatusCode == HttpStatusCode.OK;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(context.MessageContentType, response.Content.Headers.ContentType.ToString(), "ContentType mismatch");
            Assert.NotNull(response.Content.Headers.Expires, "Expires header missing");

            Assert.GreaterOrEqual(response.Content.Headers.Expires.Value, DateTimeOffset.UtcNow.AddDays(360), "Unexpected Expires datetime year value");

            Assert.NotNull(response.Content.Headers.ContentLength, "ContentLength not set");

            Assert.AreEqual(context.MessageBody.Length, response.Content.Headers.ContentLength.Value, "ContentLength mismatch");

            var body = await response.Content.ReadAsByteArrayAsync();

            Assert.AreEqual(context.MessageBody, body, "Body bytes mismatch");

            Assert.NotNull(response.Headers.GetValues("ETag").SingleOrDefault(), "Etag not set");
        }

        private string addressOfAuditInstance = "TODO";

        class MyContext : ScenarioContext
        {
            public string AuditInstanceMessageId { get; set; }
            public byte[] MessageBody { get; set; }
            public string MessageContentType { get; set; }
            public bool MessageAudited { get; set; }
            public string AuditInstanceId { get; set; }
        }

        class MyMessage : ICommand
        {
            public Guid Id { get; set; } = Guid.NewGuid();
        }

        class RemoteEndpoint : EndpointConfigurationBuilder
        {
            public RemoteEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c => { c.RegisterComponents(cc => cc.ConfigureComponent<MessageBodySpy>(DependencyLifecycle.SingleInstance)); });
            }

            public class MessageBodySpy : IMutateIncomingTransportMessages
            {
                public MyContext Context { get; set; }

                public Task MutateIncoming(MutateIncomingTransportMessageContext context)
                {
                    Context.MessageContentType = context.Headers[Headers.ContentType];
                    Context.MessageBody = context.Body;
                    return Task.FromResult(0);
                }
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.AuditInstanceMessageId = context.MessageId;
                    return Task.FromResult(0);
                }
            }
        }
    }
}