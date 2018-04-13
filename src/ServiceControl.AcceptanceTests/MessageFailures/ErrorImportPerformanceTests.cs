namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    public class ErrorImportPerformanceTests : AcceptanceTest
    {
        [Test]
        public async Task Should_import_all_messages()
        {
            var context = new MyContext();


            await Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus =>
                {
                    Parallel.For(0, 100, i =>
                        bus.SendLocal(new MyMessage())
                    );
                }))
                .Done(async c =>
                {
                    var result = await TryGetMany<MessagesView>("/api/messages?per_page=150");
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
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>());
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {

                public void Handle(MyMessage message)
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