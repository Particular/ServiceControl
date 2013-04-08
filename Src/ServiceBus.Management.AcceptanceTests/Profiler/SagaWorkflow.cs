using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.Config;
using NServiceBus.Saga;
using NServiceBus.Unicast.Subscriptions;
using NUnit.Framework;
using ServiceBus.Management.AcceptanceTests.Contexts;

namespace ServiceBus.Management.AcceptanceTests.Profiler
{
    public class SagaWorkflow : NServiceBusAcceptanceTest
    {
        const string AuditQueue = "audit";
        const string ErrorQueue = "error";

        [Test, Ignore] // Breakpoints in the Saga class message handlers don't hit.
        public void Generate_Messages_With_NoBuyersRemorse_For_Profiler()
        {

            Scenario.Define<Context>()
                    .WithEndpoint<ProcessOrderEndpoint>(b => b.Given((bus, context) =>
                        {
                            if (Configure.Instance.Configurer.HasComponent<MessageDrivenSubscriptionManager>())
                            {
                                Configure.Instance.Builder.Build<MessageDrivenSubscriptionManager>().ClientSubscribed +=
                                    (sender, args) =>
                                        {
                                            lock (context)
                                            {
                                                context.NumberOfSubscribers++;

                                                if (context.NumberOfSubscribers >= 2)
                                                {
                                                    context.Subscriber1Subscribed = true;
                                                    context.Subscriber2Subscribed = true;
                                                }
                                            }
                                        };
                            }
                        })
                    .When(
                        c =>
                        c.Subscriber1Subscribed && c.Subscriber2Subscribed &&
                        !c.IsSubmitOrderSent, (bus, c) =>
                            {
                                lock (c)
                                {
                                    c.IsSubmitOrderSent = true;
                                }

                                bus.SendLocal<SubmitOrder>(m =>
                                    {
                                        m.OrderId = Guid.NewGuid();
                                        m.OrderPlacedOn = DateTime.Now;
                                    });

                            }))
                     .WithEndpoint<NotificationEndpoint>(b => b.Given((bus, context) =>
                        {
                            bus.Subscribe<OrderReceived>();

                            if (!Configure.Instance.Configurer.HasComponent<MessageDrivenSubscriptionManager>())
                                context.Subscriber1Subscribed = true;
                        }))
                    .WithEndpoint<BuyersRemorseSagaEndpoint>(b => b.Given((bus, context) =>
                        {
                            bus.Subscribe<OrderReceived>();

                            if (!Configure.Instance.Configurer.HasComponent<MessageDrivenSubscriptionManager>())
                                context.Subscriber2Subscribed = true;
                        }))

                    .Done(c => c.IsSagaComplete && c.IsNotificationComplete)
                    .Run(TimeSpan.FromMinutes(3));
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

            public class ProcessOrderHandler : IHandleMessages<SubmitOrder>
            {
                public Context Context { get; set; }


                public IBus Bus { get; set; }

                public void Handle(SubmitOrder message)
                {
                    Bus.Publish<OrderReceived>(m =>
                    {
                        m.OrderId = message.OrderId;
                        m.OrderReceivedAt = DateTime.Now;
                    });
                }
            }
        }

        public class NotificationEndpoint : EndpointConfigurationBuilder
        {
            public NotificationEndpoint()
            {
                EndpointSetup<DefaultServer>()
                    .AddMapping<OrderReceived>(typeof (ProcessOrderEndpoint))
                    .AuditTo(Address.Parse(AuditQueue));
            }

            public class NotifyOrderReceivedHandler : IHandleMessages<OrderReceived> 
            {
                public Context Context { get; set; }
                public void Handle(OrderReceived message)
                {
 	                // Send email to whoever needs to know!
                    Context.IsNotificationComplete = true;
                }
            }
        }

        public class BuyersRemorseSagaEndpoint : EndpointConfigurationBuilder
        {
            public BuyersRemorseSagaEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c.UseRavenTimeoutPersister()
                    .RavenSagaPersister()
                    .UnicastBus()
                    .DoNotAutoSubscribe()
                    .DoNotAutoSubscribeSagas())
                    .AddMapping<OrderReceived>(typeof(ProcessOrderEndpoint))
                    .AuditTo(Address.Parse(AuditQueue));
            }

            public class BuyersRemorsePolicy : Saga<BuyersRemorseSagaData>,
  IAmStartedByMessages<OrderReceived>,
  IAmStartedByMessages<CancelOrder>,
              IHandleTimeouts<CancelPolicyTimeout>
            {

                public Context Context { get; set; }

                public void Handle(OrderReceived message)
                {  
                    if (Data.Cancelled)
                    {
                        MarkAsComplete();
                        Context.IsSagaComplete = true;
                    }
                    else
                    {
                        Data.OrderId = message.OrderId;
                        Data.ProductIds = message.ProductIds;
                        RequestTimeout<CancelPolicyTimeout>(TimeSpan.FromMinutes(1));
                    }

                    Data.Submitted = true;
                }

                public void Handle(CancelOrder message)
                {
                    if (Data.Submitted)
                    {
                        MarkAsComplete();
                        Context.IsSagaComplete = true;
                    }
                    else
                        Data.OrderId = message.OrderId;

                    Data.Cancelled = true;
                }

                public void Timeout(CancelPolicyTimeout state)
                {
                    Bus.Publish<OrderAccepted>(m => m.OrderId = Data.OrderId);
                }

                public override void ConfigureHowToFindSaga()
                {
                    ConfigureMapping<OrderReceived>(s => s.OrderId, m => m.OrderId);
                    ConfigureMapping<CancelOrder>(s => s.OrderId, m => m.OrderId);
                }
            }

            public class BuyersRemorseSagaData : IContainSagaData
            {
                public Guid Id { get; set; }
                public string Originator { get; set; }
                public string OriginalMessageId { get; set; }

                [Unique]
                public Guid OrderId { get; set; }
                public List<Guid> ProductIds { get; set; }
                public bool Cancelled { get; set; }
                public bool Submitted { get; set; }
            }

            public class CancelPolicyTimeout
            {
            }
        }

        [Serializable]
        public class SubmitOrder : ICommand
        {
            public Guid OrderId { get; set; }
            public DateTime OrderPlacedOn { get; set; }
            public List<Guid> ProductIds { get; set; }
            public string Content { get; set; }
        }

        [Serializable]
        public class CancelOrder : ICommand
        {
            public Guid OrderId { get; set; }
            public DateTime OrderCancelRequestReceivedOn { get; set; }
        }

        [Serializable]
        public class OrderReceived : IEvent
        {
            public Guid OrderId { get; set; }
            public DateTime OrderReceivedAt { get; set; }
            public List<Guid> ProductIds { get; set; }
        }

        [Serializable]
        public class OrderAccepted : IEvent
        {
            public Guid OrderId { get; set; }
            public DateTime OrderAcceptedAt { get; set; }
        }

        public class Context : ScenarioContext
        {
            public bool IsSubmitOrderSent { get; set; }

            public bool IsSagaComplete { get; set; }
            public bool IsNotificationComplete { get; set; }

            public bool Subscriber1Subscribed { get; set; }
            public bool Subscriber2Subscribed { get; set; }

            public int NumberOfSubscribers { get; set; }
        }
    }
}
