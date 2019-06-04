namespace ServiceBus.Management.AcceptanceTests.Auditing
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Settings;

    class When_message_searched_by_messages : AcceptanceTest
    {
        [Test]
        public async Task Should_be_found()
        {
            var response = new List<MessagesView>();

            var context = await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When(async (bus, c) =>
                {
                    await bus.Send(new MyMessage());
                    await bus.SendLocal(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MessagesView>("/api/messages/", instanceName: ServiceControlInstanceName);
                    response = result;
                    return result && response.Count == 2;
                })
                .Run();

            var expectedMasterInstanceId = InstanceIdGenerator.FromApiUrl(SettingsPerInstance[ServiceControlInstanceName].ApiUrl);
            var expectedAuditInstanceId = InstanceIdGenerator.FromApiUrl(SettingsPerInstance[ServiceControlAuditInstanceName].ApiUrl);

            Assert.AreNotEqual(expectedMasterInstanceId, expectedAuditInstanceId);

            var sentMessage = response.SingleOrDefault(msg => msg.MessageId == context.SentMessageId);

            Assert.NotNull(sentMessage, "Sent message not found");
            Assert.AreEqual(expectedAuditInstanceId, sentMessage.InstanceId, "Audit instance id mismatch");

            var sentLocalMessage = response.SingleOrDefault(msg => msg.MessageId == context.SentLocalMessageId);

            Assert.NotNull(sentLocalMessage, "Sent local message not found");
            Assert.AreEqual(expectedAuditInstanceId, sentLocalMessage.InstanceId, "Audit instance id mismatch");
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.ConfigureTransport()
                        .Routing()
                        .RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.SentLocalMessageId = context.MessageId;
                    return Task.FromResult(0);
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>(c => { });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.SentMessageId = context.MessageId;
                    return Task.FromResult(0);
                }
            }
        }


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string SentMessageId { get; set; }
            public string SentLocalMessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}