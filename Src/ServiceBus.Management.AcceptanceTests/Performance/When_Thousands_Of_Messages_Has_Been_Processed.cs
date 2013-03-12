namespace ServiceBus.Management.AcceptanceTests.Performance
{
    using System;
    using System.Diagnostics;
    using System.Messaging;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Config;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    [TestFixture]
    public class When_Thousands_Of_Messages_Has_Been_Processed : HttpUtil
    {
        const string AuditQueue = "audit";
        const string ErrorQueue = "error";
        const string BreakWhenContentContains = "66";
        const int MaxLoadToGenerate = 1000;
        [Test]
        public void Generate_Load_Test()
        {
            var context = new MyContext();
            Scenario.Define(() => context)
                    .WithEndpoint<ProcessOrderEndpoint>(condition => condition.Given(bus =>
                        {
                            int index = 0;
                            Parallel.For(index, MaxLoadToGenerate,
                                         (s, u) =>
                                             {
                                                 bus.SendLocal(new ProcessOrder
                                                     {
                                                         OrderId = Guid.NewGuid(),
                                                         OrderPlacedOn = DateTime.Now,
                                                         Content = index.ToString()
                                                     });
                                                 index++;
                                             });
                        }))
                    .Done(c => c.IsProcessingComplete)
                    .Run();

        }

        public class ProcessOrderEndpoint : EndpointConfigurationBuilder
        {
            public ProcessOrderEndpoint()
            {
                EndpointSetup<DefaultServer>()
                    .WithConfig<TransportConfig>(c => c.MaxRetries = 0)
                    .WithConfig<MessageForwardingInCaseOfFaultConfig>(e => e.ErrorQueue = ErrorQueue)
                    .WithConfig<SecondLevelRetriesConfig>(slr => slr.Enabled = false)
                    .AuditTo(Address.Parse(AuditQueue));

            }

            public class ProcessOrderHandler : IHandleMessages<ProcessOrder>
            {
                public MyContext Context { get; set; }

              
                public IBus Bus { get; set; }
                static int numberOfMessagesProcessed;

                public void Handle(ProcessOrder message)
                {
                    var current = Interlocked.Increment(ref numberOfMessagesProcessed);
                    if (current >= MaxLoadToGenerate)
                    {
                        Context.IsProcessingComplete = true;
                    }

                    if (!string.IsNullOrEmpty(BreakWhenContentContains) && message.Content.Contains(BreakWhenContentContains))
                    {
                        throw new ArgumentException("Message content contains the configured error string : {0}", BreakWhenContentContains);
                    }

                    Bus.Publish<OrderReceived>(m =>
                    {
                        m.OrderId = message.OrderId;
                        m.OrderReceivedAt = DateTime.Now;
                    });

                }
            }
        }

        public class ProcessOrderAcceptedEndpoint: EndpointConfigurationBuilder
        {
            
        }
        
        public class ProcessOrder : ICommand
        {
            public Guid OrderId { get; set; }
            public DateTime OrderPlacedOn { get; set; }
            public string Content { get; set; }
        }

        public class OrderReceived : IEvent
        {
            public Guid OrderId { get; set; }
            public DateTime OrderReceivedAt { get; set; }
        }
        
        public class MyContext : ScenarioContext
        {
            public bool IsProcessingComplete { get; set; }
        }
    }
}
