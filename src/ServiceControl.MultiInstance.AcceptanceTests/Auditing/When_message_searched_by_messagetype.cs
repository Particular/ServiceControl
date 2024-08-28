namespace ServiceControl.MultiInstance.AcceptanceTests.Auditing
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using CompositeViews.Messages;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.Settings;
    using TestSupport;

    class When_message_searched_by_messagetype : AcceptanceTest
    {
        [Test]
        public async Task Should_be_found()
        {
            var response = new List<MessagesView>();

            //search for the message type
            var searchString = nameof(MyMessage);

            var context = await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When(async (bus, c) =>
                {
                    await bus.Send(new MyMessage());
                    await bus.SendLocal(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MessagesView>("/api/messages/search/" + searchString, instanceName: ServiceControlInstanceName);
                    response = result;
                    return result && response.Count == 2;
                })
                .Run();

            var expectedMasterInstanceId = InstanceIdGenerator.FromApiUrl(SettingsPerInstance[ServiceControlInstanceName].RootUrl);
            var expectedAuditInstanceId = InstanceIdGenerator.FromApiUrl(SettingsPerInstance[ServiceControlAuditInstanceName].RootUrl);

            Assert.That(expectedMasterInstanceId, Is.Not.EqualTo(expectedAuditInstanceId));

            var sentMessage = response.SingleOrDefault(msg => msg.MessageId == context.SentMessageId);

            Assert.That(sentMessage, Is.Not.Null, "Sent message not found");
            Assert.That(expectedAuditInstanceId, Is.EqualTo(sentMessage.InstanceId), "Audit instance id mismatch");

            var sentLocalMessage = response.SingleOrDefault(msg => msg.MessageId == context.SentLocalMessageId);

            Assert.That(sentLocalMessage, Is.Not.Null, "Sent local message not found");
            Assert.That(expectedAuditInstanceId, Is.EqualTo(sentLocalMessage.InstanceId), "Audit instance id mismatch");
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.ConfigureRouting()
                        .RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly MyContext testContext;
                readonly IReadOnlySettings settings;

                public MyMessageHandler(MyContext testContext, IReadOnlySettings settings)
                {
                    this.testContext = testContext;
                    this.settings = settings;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    testContext.SentLocalMessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithAudit>(c => { });

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly MyContext testContext;
                readonly IReadOnlySettings settings;

                public MyMessageHandler(MyContext testContext, IReadOnlySettings settings)
                {
                    this.testContext = testContext;
                    this.settings = settings;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    testContext.SentMessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string SentLocalMessageId { get; set; }
            public string SentMessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}