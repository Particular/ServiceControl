﻿namespace ServiceControl.MultiInstance.AcceptanceTests.Auditing
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
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_message_searched_by_endpoint_by_messagetype : AcceptanceTest
    {
        [Test]
        public async Task Should_be_found()
        {
            var response = new List<MessagesView>();

            var endpointName = Conventions.EndpointNamingConvention(typeof(ReceiverRemote));

            //search for the message type
            var searchString = nameof(MyMessage);

            var context = await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage())))
                .WithEndpoint<ReceiverRemote>()
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MessagesView>($"/api/endpoints/{endpointName}/messages/search/" + searchString, instanceName: ServiceControlInstanceName);
                    response = result;
                    return result && response.Count == 1;
                })
                .Run();

            var expectedRemote1InstanceId = InstanceIdGenerator.FromApiUrl(SettingsPerInstance[ServiceControlAuditInstanceName].RootUrl);

            var remote1Message = response.SingleOrDefault(msg => msg.MessageId == context.Remote1MessageId);

            Assert.That(remote1Message, Is.Not.Null, "Remote1 message not found");
            Assert.AreEqual(expectedRemote1InstanceId, remote1Message.InstanceId, "Remote1 instance id mismatch");
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.ConfigureRouting()
                        .RouteToEndpoint(typeof(MyMessage), typeof(ReceiverRemote));
                });
            }
        }

        public class ReceiverRemote : EndpointConfigurationBuilder
        {
            public ReceiverRemote()
            {
                EndpointSetup<DefaultServerWithAudit>(c => { });
            }

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
                    testContext.Remote1MessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string Remote1MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}