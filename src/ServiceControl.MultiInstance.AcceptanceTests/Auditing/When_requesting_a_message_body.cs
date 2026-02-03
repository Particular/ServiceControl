namespace ServiceControl.MultiInstance.AcceptanceTests.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using CompositeViews.Messages;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.Settings;
    using TestSupport;

    class When_requesting_a_message_body : AcceptanceTest
    {
        [Test]
        [CancelAfter(120_000)]
        public async Task Should_be_forwarded_to_audit_instance(CancellationToken cancellationToken)
        {
            string addressOfAuditInstance = null;

            CustomServiceControlAuditSettings = s => addressOfAuditInstance = s.ApiUrl;

            HttpResponseMessage response = null;
            MessagesView capturedMessage = null;

            var context = await Define<MyContext>()
                .WithEndpoint<RemoteEndpoint>(b => b.When(async (bus, ctx) =>
                {
                    Assert.That(string.IsNullOrEmpty(addressOfAuditInstance), Is.False);

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

                    response = await this.GetRaw($"/api{capturedMessage.BodyUrl}", ServiceControlInstanceName);
                    Console.WriteLine($"GetRaw for {c.AuditInstanceMessageId} resulted in {response.StatusCode}");
                    return response.StatusCode == HttpStatusCode.OK;
                })
                .Run(cancellationToken);

            Assert.That(response.Content.Headers.ContentType.ToString(), Is.EqualTo(context.MessageContentType), "ContentType mismatch");

            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(body, Is.EqualTo(context.MessageBody), "Body bytes mismatch");
                Assert.That(response.Headers.GetValues("ETag").SingleOrDefault(), Is.Not.Null, "Etag not set");
            });
        }

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
            public RemoteEndpoint() => EndpointSetup<DefaultServerWithAudit>(c => c.RegisterComponents(services => services.AddSingleton<IMutateIncomingTransportMessages, MessageBodySpy>()));

            public class MessageBodySpy : IMutateIncomingTransportMessages
            {
                readonly MyContext testContext;

                public MessageBodySpy(MyContext testContext) => this.testContext = testContext;

                public Task MutateIncoming(MutateIncomingTransportMessageContext context)
                {
                    testContext.MessageContentType = context.Headers[Headers.ContentType];
                    testContext.MessageBody = context.Body.ToArray();
                    return Task.CompletedTask;
                }
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly MyContext testContext;

                public MyMessageHandler(MyContext testContext) => this.testContext = testContext;

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.AuditInstanceMessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }
    }
}