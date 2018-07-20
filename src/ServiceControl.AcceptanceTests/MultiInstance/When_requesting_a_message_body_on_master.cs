namespace ServiceBus.Management.AcceptanceTests.MultiInstance
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Settings;

    public class When_requesting_a_message_body_on_master : AcceptanceTest
    {
        [Test]
        public async Task Should_be_forwarded_to_remote()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            HttpResponseMessage response = null;
            MessagesView capturedMessage = null;

            var context = await Define<MyContext>(Remote1, Master)
                .WithEndpoint<RemoteEndpoint>(b => b.When(async (bus, ctx) =>
                {
                    ctx.Remote1InstanceId = InstanceIdGenerator.FromApiUrl(addressOfRemote);
                    await bus.SendLocal(new MyMessage());
                }))
                .Done(async c =>
                {
                    if (string.IsNullOrWhiteSpace(c.Remote1MessageId))
                    {
                        return false;
                    }

                    if (!c.Remote1MessageAudited)
                    {
                        var result = await this.TryGetMany<MessagesView>("/api/messages", msg => msg.MessageId == c.Remote1MessageId, Master);
                        List<MessagesView> messages = result;
                        if (!result)
                        {
                            return false;
                        }

                        c.Remote1MessageAudited = true;
                        capturedMessage = messages.Single(msg => msg.MessageId == c.Remote1MessageId);
                    }

                    response = await this.GetRaw($"/api/{capturedMessage.BodyUrl}", Master);
                    Console.WriteLine($"GetRaw for {c.Remote1MessageId} resulted in {response.StatusCode}");
                    return response.StatusCode == HttpStatusCode.OK;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(context.Remote1MessageContentType, response.Content.Headers.ContentType.ToString(), "ContentType mismatch");
            Assert.NotNull(response.Content.Headers.Expires, "Expires header missing");

            Assert.GreaterOrEqual(response.Content.Headers.Expires.Value, DateTimeOffset.UtcNow.AddDays(360), "Unexpected Expires datetime year value");

            Assert.NotNull(response.Content.Headers.ContentLength, "ContentLength not set");

            Assert.AreEqual(context.Remote1MessageBody.Length, response.Content.Headers.ContentLength.Value, "ContentLength mismatch");

            var body = await response.Content.ReadAsByteArrayAsync();

            Assert.AreEqual(context.Remote1MessageBody, body, "Body bytes mismatch");

            Assert.NotNull(response.Headers.GetValues("ETag").SingleOrDefault(), "Etag not set");
        }

        private void ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues(string instanceName, Settings settings)
        {
            switch (instanceName)
            {
                case Remote1:
                    addressOfRemote = settings.ApiUrl;
                    settings.AuditQueue = AuditRemote;
                    settings.ErrorQueue = ErrorRemote;
                    break;
                case Master:
                    settings.RemoteInstances = new[]
                    {
                        new RemoteInstanceSetting
                        {
                            ApiUri = addressOfRemote,
                            QueueAddress = Remote1
                        }
                    };
                    settings.AuditQueue = AuditMaster;
                    settings.ErrorQueue = ErrorMaster;
                    break;
            }
        }

        private string addressOfRemote;
        private const string Master = "master";
        private const string Remote1 = "remote1";
        private static string AuditMaster = $"{Master}.audit";
        private static string ErrorMaster = $"{Master}.error";
        private static string AuditRemote = $"{Remote1}.audit1";
        private static string ErrorRemote = $"{Remote1}.error1";

        class MyContext : ScenarioContext
        {
            public string Remote1MessageId { get; set; }
            public byte[] Remote1MessageBody { get; set; }
            public string Remote1MessageContentType { get; set; }
            public bool Remote1MessageAudited { get; set; }
            public string Remote1InstanceId { get; set; }
        }

        class MyMessage : ICommand
        {
            public Guid Id { get; set; } = Guid.NewGuid();
        }

        class RemoteEndpoint : EndpointConfigurationBuilder
        {
            public RemoteEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.RegisterComponents(cc => cc.ConfigureComponent<MessageBodySpy>(DependencyLifecycle.SingleInstance));
                    c.AuditProcessedMessagesTo(AuditRemote);
                });
            }

            public class MessageBodySpy : IMutateIncomingTransportMessages
            {
                public MyContext Context { get; set; }

                public Task MutateIncoming(MutateIncomingTransportMessageContext context)
                {
                    Context.Remote1MessageContentType = context.Headers[Headers.ContentType];
                    Context.Remote1MessageBody = context.Body;
                    return Task.FromResult(0);
                }
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.Remote1MessageId = context.MessageId;
                    return Task.FromResult(0);
                }
            }
        }
    }
}