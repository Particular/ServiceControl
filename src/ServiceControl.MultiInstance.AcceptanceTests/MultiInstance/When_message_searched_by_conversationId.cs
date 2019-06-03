namespace ServiceBus.Management.AcceptanceTests.MultiInstance
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    class When_message_searched_by_conversationId : AcceptanceTest
    {
        [Test]
        public async Task Should_be_found()
        {
            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.SendLocal(new TriggeringMessage())))
                .WithEndpoint<ReceiverRemote>()
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MessagesView>($"/api/conversations/{c.ConversationId}", instanceName: ServiceControlInstanceName);
                    List<MessagesView> response = result;
                    return c.ConversationId != null && result && response.Count == 2;
                })
                .Run();
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.ConfigureTransport()
                        .Routing()
                        .RouteToEndpoint(typeof(TriggeredMessage), typeof(ReceiverRemote));
                });
            }

            public class TriggeringMessageHandler : IHandleMessages<TriggeringMessage>
            {
                public MyContext Context { get; set; }

                public Task Handle(TriggeringMessage message, IMessageHandlerContext context)
                {
                    Context.ConversationId = context.MessageHeaders[Headers.ConversationId];
                    return context.Send(new TriggeredMessage());
                }
            }
        }

        public class ReceiverRemote : EndpointConfigurationBuilder
        {
            public ReceiverRemote()
            {
                EndpointSetup<DefaultServerWithAudit>(c => { });
            }

            public class TriggeredMessageHandler : IHandleMessages<TriggeredMessage>
            {
                public MyContext Context { get; set; }

                public Task Handle(TriggeredMessage message, IMessageHandlerContext context)
                {
                    Context.ConversationId = context.MessageHeaders[Headers.ConversationId];
                    return Task.FromResult(0);
                }
            }
        }

        public class TriggeringMessage : ICommand
        {
        }

        public class TriggeredMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string ConversationId { get; set; }
        }
    }
}