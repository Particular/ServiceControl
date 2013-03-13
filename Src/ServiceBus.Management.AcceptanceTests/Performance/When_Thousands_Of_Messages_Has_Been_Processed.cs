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
    using NServiceBus.Unicast.Subscriptions;

    [TestFixture]
    public class When_Thousands_Of_Messages_Has_Been_Processed 
    {
        const string AuditQueue = "audit";
        const string ErrorQueue = "error";
        const string BreakWhenContentContains = "";
        const int MaxLoadToGenerate = 50;
        const int MaxTimeInSecondsForSimulatingProcessingTime = 0;
               
        [Test]
        public void Generate_Load_Test()
        {
            Scenario.Define<MyContext>()
                 .WithEndpoint<ProcessOrderReceivedEndpoint>(c =>
                     c.Given((bus, context) =>
                     {
                         bus.Subscribe<OrderReceived>();
                         context.IsOrderReceivedEndpointSubscribedToEvent = true;
                     }))
                 .WithEndpoint<ProcessOrderEndpoint>(b => 
                    b.Given((bus, context) =>
                        {
                            Configure.Instance.Builder.Build<MessageDrivenSubscriptionManager>().ClientSubscribed +=
                                       (sender, args) =>
                                       {
                                           lock (context)
                                           {
                                               context.IsClientSubscribed = true;

                                           }
                                       };

                        })
                        .When(c => c.IsOrderReceivedEndpointSubscribedToEvent && !c.IsProcessOrderSent , (bus, c) =>
                            {
                                lock (c)
                                {
                                    c.IsProcessOrderSent = true;
                                }
                                for (int index = 0; index < MaxLoadToGenerate; index++)
                                {
                                    bus.SendLocal(new ProcessOrder
                                        {
                                            OrderId = Guid.NewGuid(),
                                            OrderPlacedOn = DateTime.Now,
                                            Content = index.ToString()
                                        });
                                }
                            }
                        ))
                            
                    .Done(c => c.IsOrderProcessingComplete && c.IsOrderReceivedComplete)
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
                        lock (Context)
                        {
                            Context.IsOrderProcessingComplete = true;
                        }
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

        public class ProcessOrderReceivedEndpoint: EndpointConfigurationBuilder
        {

            public ProcessOrderReceivedEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c.UnicastBus().DoNotAutoSubscribe())
                   .WithConfig<TransportConfig>(c => c.MaxRetries = 0)
                   .WithConfig<MessageForwardingInCaseOfFaultConfig>(e => e.ErrorQueue = ErrorQueue)
                   .WithConfig<SecondLevelRetriesConfig>(slr => slr.Enabled = false)
                   .AddMapping<OrderReceived>(typeof(ProcessOrderEndpoint))
                   .AuditTo(Address.Parse(AuditQueue));
            }

            public class ProcessOrderReceivedHandler : IHandleMessages<OrderReceived>
            {
                public IBus Bus { get; set; }
  
                private static int numberOfOrderReceivedProcessed;
                public MyContext Context { get; set; }

                public void Handle(OrderReceived message)
                {
                    if (MaxTimeInSecondsForSimulatingProcessingTime > 0)
                    {
                        // Throw in a random processing time anywhere between 1 and specified processing time
                        var timeToSleep = (new Random()).Next(1, MaxTimeInSecondsForSimulatingProcessingTime);
                        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(timeToSleep));
                    }

                    Bus.Publish<OrderAccepted>(m =>
                    {
                        m.OrderId = message.OrderId;
                        m.OrderAcceptedAt = DateTime.Now;
                    });

                    var current = Interlocked.Increment(ref numberOfOrderReceivedProcessed);
                    if (current >= MaxLoadToGenerate)
                    {
                        lock (Context)
                        {
                            Context.IsOrderReceivedComplete = true;
                        }
                    }
                }
            }
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

        public class OrderAccepted : IEvent
        {
            public Guid OrderId { get; set; }
            public DateTime OrderAcceptedAt { get; set; }
        }
        
        public class MyContext : ScenarioContext
        {
            public bool IsOrderReceivedEndpointSubscribedToEvent { get; set; }
            public bool IsOrderProcessingComplete { get; set; }
            public bool IsOrderReceivedComplete { get; set; }
            public bool IsClientSubscribed { get; set; }
            public bool IsProcessOrderSent { get; set; }
        }
    }
}
