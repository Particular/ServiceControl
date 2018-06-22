namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.CompositeViews.Messages;

    public class ErrorImportPerformanceTests : AcceptanceTest
    {
        [Test]
        public async Task Should_import_all_messages()
        {
            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(bus => Task.WhenAll(Enumerable.Repeat(0, 100).Select(i => bus.SendLocal(new MyMessage())))).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MessagesView>("/api/messages?per_page=150");
                    if (!result)
                    {
                        return false;
                    }

                    List<MessagesView> messages = result;
                    if (messages.Count < 100)
                    {
                        Console.Out.WriteLine("Messages found: " + messages.Count);
                    }

                    return messages.Count >= 100;
                })
                .Run(TimeSpan.FromMinutes(3));
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.Recoverability().Delayed(s => s.NumberOfRetries(0)));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    throw new Exception("Simulated exception");
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {

        }
    }
}