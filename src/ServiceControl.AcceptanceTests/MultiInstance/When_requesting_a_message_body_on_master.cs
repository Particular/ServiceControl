namespace ServiceBus.Management.AcceptanceTests.MultiInstance
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Settings;

    public class When_requesting_a_message_body_on_master : AcceptanceTest
    {
        private const string Master = "master";
        private static string AuditMaster = $"{Master}.audit";
        private static string ErrorMaster = $"{Master}.error";
        private const string Remote1 = "remote1";
        private static string AuditRemote = $"{Remote1}.audit1";
        private static string ErrorRemote = $"{Remote1}.error1";

        private string addressOfRemote;

        [Test]
        public async Task Should_be_forwarded_to_remote()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();

            HttpResponseMessage response = null;
            MessagesView capturedMessage = null;
            
            await Define(context, Remote1, Master)
                .WithEndpoint<RemoteEndpoint>(b => b.Given(bus =>
                {
                    context.Remote1InstanceId = InstanceIdGenerator.FromApiUrl(addressOfRemote);
                    bus.SendLocal(new MyMessage());
                }))
                .Done(async c =>
                {
                    if (string.IsNullOrWhiteSpace(context.Remote1MessageId))
                    {
                        return false;
                    }

                    if (!c.Remote1MessageAudited)
                    {
                        var result = await TryGetMany<MessagesView>("/api/messages", msg => msg.MessageId == c.Remote1MessageId, Master);
                        List<MessagesView> messages = result;
                        if (!result)
                        {
                            return false;
                        }
                        c.Remote1MessageAudited = true;
                        capturedMessage = messages.Single(msg => msg.MessageId == c.Remote1MessageId);
                    }

                    response = await GetRaw($"/api/{capturedMessage.BodyUrl}", Master);
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
                    settings.AuditQueue = Address.Parse(AuditRemote);
                    settings.ErrorQueue = Address.Parse(ErrorRemote);
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
                    settings.AuditQueue = Address.Parse(AuditMaster);
                    settings.ErrorQueue = Address.Parse(ErrorMaster);
                    break;
            }
        }

        class MyContext : ScenarioContext {
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
                    c.RegisterComponents(cc => cc.ConfigureComponent<MessageBodySpy>(DependencyLifecycle.SingleInstance)))
                    .AuditTo(Address.Parse(AuditRemote));
            }

            public class MessageBodySpy : IMutateIncomingTransportMessages
            {
                public MyContext Context { get; set; }
                public void MutateIncoming(TransportMessage transportMessage)
                {
                    Context.Remote1MessageContentType = transportMessage.Headers[Headers.ContentType];
                    Context.Remote1MessageBody = transportMessage.Body;
                }
            }

        public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.Remote1MessageId = Bus.CurrentMessageContext.Id;
                }
            }
        }
    }
}
