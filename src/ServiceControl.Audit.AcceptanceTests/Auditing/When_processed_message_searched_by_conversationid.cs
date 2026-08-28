namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Settings;
    using NUnit.Framework;

    class When_processed_message_searched_by_conversationid : AcceptanceTest
    {
        [Test]
        public async Task Should_be_found() =>
            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage())))
                .WithEndpoint<Receiver>()
                .Done(async c => c.ConversationId != null && await this.TryGetMany<MessagesView>("/api/messages/search/" + c.ConversationId))
                .Run();

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithAudit>();

            [Handler]
            public class MyMessageHandler(MyContext testContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    testContext.ConversationId = context.MessageHeaders[Headers.ConversationId];
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : ICommand;

        public class MyContext : ScenarioContext
        {
            public string ConversationId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}