namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Audit.Auditing.MessagesView;
    using TestSupport.EndpointTemplates;

    class When_message_ids_has_special_characters : AcceptanceTest
    {
        [TestCase(":")]
        [TestCase("_")]
        public async Task Should_be_found_in_search(string separator)
        {
            var numMessages = 10;
            var messageIdBase = Guid.NewGuid();

            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When(async (bus, c) =>
                {
                    for (int i = 0; i < numMessages; i++)
                    {
                        var options = new SendOptions();
                        options.SetMessageId($"{messageIdBase}{separator}{i}");

                        await bus.Send(new MyMessage(), options);
                    }
                }))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    if (c.NumMessages != numMessages)
                    {
                        return false;
                    }
                    var allResults = await this.TryGetMany<MessagesView>("/api/messages/");

                    if (allResults.Items.Count != numMessages)
                    {
                        return false;
                    }

                    var firstMessageId = messageIdBase + separator + "0";

                    var results = await this.TryGetMany<MessagesView>("/api/messages/search/" + firstMessageId);


                    Assert.AreEqual(1, results.Items.Count);
                    Assert.AreEqual(firstMessageId, results.Items.Single().MessageId);

                    return true;
                })
                .Run();

        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.NumMessages++;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public int NumMessages { get; set; }
        }
    }
}