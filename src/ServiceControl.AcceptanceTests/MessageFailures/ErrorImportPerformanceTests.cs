namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    public class ErrorImportPerformanceTests : AcceptanceTest
    {

        [Test]
        public void Should_import_all_messages()
        {
            var context = new MyContext();


            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus =>
                {
                    for (var i = 0; i < 100; i++)
                    {
                        bus.SendLocal(new MyMessage());
                    }
                }))
                .Done(c =>
                {
                    List<MessagesView> messages;

                    if (!TryGetMany("/api/messages?per_page=150", out messages))
                    {
                        return false;
                    }

                    var numResults = messages.Count();

                    if (numResults < 100)
                    {
                        Console.Out.WriteLine("Messages found: " + messages.Count());
                  
                        Thread.Sleep(2000);
                    }

                    return messages.Count() >= 100;
                })
                .Run();

        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c=>Configure.Features.Disable<SecondLevelRetries>())
                    .AuditTo(Address.Parse("audit"));
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