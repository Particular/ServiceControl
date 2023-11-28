namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;

    class When_processed_message_searched_by_msgid_for_endpoint : AcceptanceTest
    {
        [Test]
        public async Task Should_be_found() =>
            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage())))
                .WithEndpoint<Receiver>()
                .Done(async c => c.MessageId != null && await this.TryGetMany<MessagesView>($"/api/endpoints/{c.EndpointNameOfReceivingEndpoint}/messages/search/{c.MessageId}"))
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

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                MyContext testContext;
                IReadOnlySettings settings;

                public MyMessageHandler(MyContext testContext, IReadOnlySettings settings)
                {
                    this.testContext = testContext;
                    this.settings = settings;
                }

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
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}