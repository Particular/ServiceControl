namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using CompositeViews.Messages;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NUnit.Framework;

    class When_failed_message_searched_by_body_content : AcceptanceTest
    {
        [Test]
        public async Task Should_be_found()
        {
            var searchString = "forty-two";

            var context = await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage
                {
                    Something = "Somewhere in the body is the answer to all of the questions. forty-two"
                })))
                .WithEndpoint<Receiver>(b => b.DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    if (c.MessageId != null && await this.TryGetMany<MessagesView>($"/api/messages/search/{c.MessageId}"))
                    {
                        c.MessageIngested = true;
                    }

                    if (!c.MessageIngested)
                    {
                        return false;
                    }
                    c.MessageFound = await this.TryGetMany<MessagesView>($"/api/messages/search/{searchString}");
                    return true;
                })
                .Run();

            Assert.That(context.MessageFound, Is.True);
        }

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
            public Receiver() =>
                EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            [Handler]
            public class MyMessageHandler(MyContext scenarioContext) : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.MessageId = context.MessageId;
                    throw new Exception("Simulated exception");
                }
            }
        }

        public class MyMessage : ICommand
        {
            public string Something { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public bool MessageIngested { get; set; }

            public bool MessageFound { get; set; }
        }
    }
}