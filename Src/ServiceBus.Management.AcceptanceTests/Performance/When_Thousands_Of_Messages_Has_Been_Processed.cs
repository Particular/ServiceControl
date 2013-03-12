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
        const string auditQueue = "audit";
        const string errorQueue = "error";
        const string breakWhenContentContains = "66";
        const int maxLoadToGenerate = 1000;
        [Test]
        public void Generate_Load_Test()
        {
            var context = new MyContext();
            Scenario.Define(() => context)
                    .WithEndpoint<ProcessOrderEndpoint>(condition => condition.Given(bus =>
                        {
                            int index = 0;
                            Parallel.For(index, maxLoadToGenerate,
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

        #region ProcessOrder endpoint
        public class ProcessOrderEndpoint : EndpointConfigurationBuilder
        {
            public ProcessOrderEndpoint()
            {
                EndpointSetup<DefaultServer>()
                    .WithConfig<TransportConfig>(c => c.MaxRetries = 0)
                    .WithConfig<MessageForwardingInCaseOfFaultConfig>(e => e.ErrorQueue = errorQueue)
                    .WithConfig<SecondLevelRetriesConfig>(slr => slr.Enabled = false)
                    .AuditTo(Address.Parse(auditQueue));

            }

            public class ProcessOrderHandler : IHandleMessages<ProcessOrder>
            {
                public MyContext Context { get; set; }

              
                public IBus Bus { get; set; }
                static int numberOfMessagesProcessed;

                public void Handle(ProcessOrder message)
                {
                    var current = Interlocked.Increment(ref numberOfMessagesProcessed);
                    if (current >= maxLoadToGenerate)
                    {
                        Context.IsProcessingComplete = true;
                    }

                    if (!string.IsNullOrEmpty(breakWhenContentContains) && message.Content.Contains(breakWhenContentContains))
                    {
                        throw new ArgumentException("Message content contains the configured error string : {0}", breakWhenContentContains);
                    }

                    Bus.Publish<OrderReceived>(m =>
                    {
                        m.OrderId = message.OrderId;
                        m.OrderReceivedAt = DateTime.Now;
                    });

                }
            }
        }
        #endregion

        #region Commands & Events used in the acceptance test
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
        #endregion

        public class MyContext : ScenarioContext
        {
            public bool IsProcessingComplete { get; set; }
        }
    }
}
