
namespace ServiceBus.Management.AcceptanceTests.Profiler
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
    public class AddLinearFlowOfMessagesToAuditQueue
    {
        const string AuditQueue = "audit";
        const string ErrorQueue = "error";
        const int MaxLoadToGenerateForStartMessage = 10;
        const int MaxTimeInSecondsForSimulatingProcessingTime = 10;
        static readonly TimeSpan MaxTimeToWaitBeforeAbortingTest = new TimeSpan(0, 0, 5, 0);


        [Test]
        public void Generate_Load_Test_For_LinearFlow()
        {
            Scenario.Define<MyContext>()
                 .WithEndpoint<ProcessOrderBilledEndpoint>(c =>
                     c.Given((bus, context) =>
                     {
                         Configure.Instance.Builder.Build<MessageDrivenSubscriptionManager>().ClientSubscribed +=
                                       (sender, args) =>
                                       {
                                           lock (context)
                                           {
                                               context.IsSubscriptionCompleteForOrderBilledEndpoint = true;
                                           }
                                       };
                         bus.Subscribe<OrderBilled>();
                         context.IsOrderBilledEndpointSubscribedToEvent = true;
                     }))
                 .WithEndpoint<ProcessOrderAcceptedEndpoint>(c =>
                     c.Given((bus, context) =>
                     {
                         Configure.Instance.Builder.Build<MessageDrivenSubscriptionManager>().ClientSubscribed +=
                                       (sender, args) =>
                                       {
                                           lock (context)
                                           {
                                               context.IsSubscriptionCompleteForOrderAcceptedEndpoint = true;
                                           }
                                       };
                         bus.Subscribe<OrderAccepted>();
                         context.IsOrderAcceptedEndpointSubscribedToEvent = true;
                     }))
                 .WithEndpoint<ProcessOrderReceivedEndpoint>(c =>
                     c.Given((bus, context) =>
                     {
                         Configure.Instance.Builder.Build<MessageDrivenSubscriptionManager>().ClientSubscribed +=
                                       (sender, args) =>
                                       {
                                           lock (context)
                                           {
                                               context.IsSubscriptionCompleteForOrderReceivedEndpoint = true;
                                           }
                                       };
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
                                           context.IsSubscriptionCompleteForProcessOrderEndpoint = true;
                                       }
                                   };

                    })
                        .When(c => c.IsOrderReceivedEndpointSubscribedToEvent && !c.IsProcessOrderSent, (bus, c) =>
                        {
                            lock (c)
                            {
                                c.IsProcessOrderSent = true;
                            }
                            for (int index = 0; index < MaxLoadToGenerateForStartMessage; index++)
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

                    .Done(c => c.AreAllOrdersShipped)
                    .Run(MaxTimeToWaitBeforeAbortingTest);

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

                public void Handle(ProcessOrder message)
                {
                    Bus.Publish<OrderReceived>(m =>
                    {
                        m.OrderId = message.OrderId;
                        m.OrderReceivedAt = DateTime.Now;
                    });
                }
            }
        }

        public class ProcessOrderReceivedEndpoint : EndpointConfigurationBuilder
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

                }
            }
        }

        public class ProcessOrderAcceptedEndpoint : EndpointConfigurationBuilder
        {
            public ProcessOrderAcceptedEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c.UnicastBus().DoNotAutoSubscribe())
                   .WithConfig<TransportConfig>(c => c.MaxRetries = 0)
                   .WithConfig<MessageForwardingInCaseOfFaultConfig>(e => e.ErrorQueue = ErrorQueue)
                   .WithConfig<SecondLevelRetriesConfig>(slr => slr.Enabled = false)
                   .AddMapping<OrderAccepted>(typeof(ProcessOrderReceivedEndpoint))
                   .AuditTo(Address.Parse(AuditQueue));
            }

            public class ProcessOrderAcceptedHandler : IHandleMessages<OrderAccepted>
            {
                public IBus Bus { get; set; }

                public MyContext Context { get; set; }

                public void Handle(OrderAccepted message)
                {
                    if (MaxTimeInSecondsForSimulatingProcessingTime > 0)
                    {
                        // Throw in a random processing time anywhere between 1 and specified processing time
                        var timeToSleep = (new Random()).Next(1, MaxTimeInSecondsForSimulatingProcessingTime);
                        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(timeToSleep));
                    }

                    Bus.Publish<OrderBilled>(m =>
                    {
                        m.OrderId = message.OrderId;
                        m.OrderBilledAt = DateTime.Now;
                    });
                }
            }

        }

        public class ProcessOrderBilledEndpoint : EndpointConfigurationBuilder
        {
            public ProcessOrderBilledEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c.UnicastBus().DoNotAutoSubscribe())
                   .WithConfig<TransportConfig>(c => c.MaxRetries = 0)
                   .WithConfig<MessageForwardingInCaseOfFaultConfig>(e => e.ErrorQueue = ErrorQueue)
                   .WithConfig<SecondLevelRetriesConfig>(slr => slr.Enabled = false)
                   .AddMapping<OrderBilled>(typeof(ProcessOrderAcceptedEndpoint))
                   .AuditTo(Address.Parse(AuditQueue));
            }

            public class ProcessOrderBilledHandler : IHandleMessages<OrderBilled>
            {
                public IBus Bus { get; set; }

                private static int numberOfOrderBilledProcessed;
                public MyContext Context { get; set; }

                public void Handle(OrderBilled message)
                {
                    if (MaxTimeInSecondsForSimulatingProcessingTime > 0)
                    {
                        // Throw in a random processing time anywhere between 1 and specified processing time
                        var timeToSleep = (new Random()).Next(1, MaxTimeInSecondsForSimulatingProcessingTime);
                        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(timeToSleep));
                    }

                    Bus.Publish<OrderShipped>(m =>
                    {
                        m.OrderId = message.OrderId;
                        m.OrderShippedAt = DateTime.Now;
                    });

                    var current = Interlocked.Increment(ref numberOfOrderBilledProcessed);
                    if (current >= MaxLoadToGenerateForStartMessage)
                    {
                        lock (Context)
                        {
                            Context.AreAllOrdersShipped = true;
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

        public class OrderBilled : IEvent
        {
            public Guid OrderId { get; set; }
            public DateTime OrderBilledAt { get; set; }
        }

        public class OrderShipped : IEvent
        {
            public Guid OrderId { get; set; }
            public DateTime OrderShippedAt { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public bool IsOrderReceivedEndpointSubscribedToEvent { get; set; }
            public bool IsOrderAcceptedEndpointSubscribedToEvent { get; set; }
            public bool IsOrderBilledEndpointSubscribedToEvent { get; set; }


            public bool AreAllOrdersShipped { get; set; }
     
            public bool IsSubscriptionCompleteForProcessOrderEndpoint { get; set; }
            public bool IsSubscriptionCompleteForOrderReceivedEndpoint { get; set; }
            public bool IsSubscriptionCompleteForOrderAcceptedEndpoint { get; set; }
            public bool IsSubscriptionCompleteForOrderBilledEndpoint { get; set; }


            // Hack to make sure When doesn't fire many times.
            public bool IsProcessOrderSent { get; set; }


        }
    }
}

