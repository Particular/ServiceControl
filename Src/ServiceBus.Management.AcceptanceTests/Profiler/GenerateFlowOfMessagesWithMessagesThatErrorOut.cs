
namespace ServiceBus.Management.AcceptanceTests.Profiler
{
    using System;
    using System.Threading;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    [TestFixture]
    public class Generate_Messages_With_Messages_That_Endup_In_The_ErrorQueue
    {
        const string AuditQueue = "audit";
        const string ErrorQueue = "error";
        const int MaxLoadToGenerateForStartMessage = 5;
        const int MaxTimeInSecondsForSimulatingProcessingTime = 10;
        static readonly TimeSpan MaxTimeToWaitBeforeAbortingTest = new TimeSpan(0, 0, 5, 0);


        [Test]
        public void Generate_Messages_With_Error_And_Wait_For_Reprocess_From_Profiler()
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

                    .Done(c => c.IsTestComplete)
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

                    var doIFail = new Random().Next(1, 11) < 3;
                    if (doIFail)
                        throw new Exception("Random exception occured in ProcessOrder!");
                   

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
                EndpointSetup<DefaultServer>(c => Configure.Features.Disable<AutoSubscribe>())
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
                    var doIFail = new Random().Next(1, 11) < 3;
                    if (doIFail)
                        throw new Exception("Random exception occured in ProcessOrderReceived Handler!");
                        
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
                EndpointSetup<DefaultServer>(c => Configure.Features.Disable<AutoSubscribe>())
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
                    var doIFail = new Random().Next(1, 11) < 3;
                    if (doIFail)
                        throw new Exception("Random exception occured in ProcessOrderAccepted!");
                   
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
                EndpointSetup<DefaultServer>(c => Configure.Features.Disable<AutoSubscribe>())
                   .WithConfig<TransportConfig>(c => c.MaxRetries = 0)
                   .WithConfig<MessageForwardingInCaseOfFaultConfig>(e => e.ErrorQueue = ErrorQueue)
                   .WithConfig<SecondLevelRetriesConfig>(slr => slr.Enabled = false)
                   .AddMapping<OrderBilled>(typeof(ProcessOrderAcceptedEndpoint))
                   .AuditTo(Address.Parse(AuditQueue));
            }

            public class ProcessOrderBilledHandler : IHandleMessages<OrderBilled>
            {
                public IBus Bus { get; set; }

                public MyContext Context { get; set; }
                private static int numberOfOrderBilledProcessed;
           
                public void Handle(OrderBilled message)
                {
                    var doIFail = new Random().Next(1, 11) < 3;
                    if (doIFail)
                        throw new Exception("Random exception occured in ProcessOrderBilled!");
                   
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
                            Context.IsTestComplete = true;
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


            public bool IsTestComplete { get; set; }

            public bool IsSubscriptionCompleteForProcessOrderEndpoint { get; set; }
            public bool IsSubscriptionCompleteForOrderReceivedEndpoint { get; set; }
            public bool IsSubscriptionCompleteForOrderAcceptedEndpoint { get; set; }
            public bool IsSubscriptionCompleteForOrderBilledEndpoint { get; set; }


            // Hack to make sure When doesn't fire many times.
            public bool IsProcessOrderSent { get; set; }


        }
    }
}

