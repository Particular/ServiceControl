namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.MessageFailures.Api;

    class When_an_event_with_multiple_subscribers_fails : AcceptanceTest
    {
        [Test]
        public void There_should_be_a_FailedMessage_for_each_subscriber()
        {
            var context = new FailingEventContext();

            var failedMessages = new List<FailedMessageView>();

            Define(context)
                .WithEndpoint<FailingSubscriber1>(behavior => behavior.Given((bus, ctx) =>
                {
                    if (ctx.HasNativePubSubSupport)
                    {
                        ctx.Subscriber1Subscribed = true;
                    }
                }))
                .WithEndpoint<FailingSubscriber2>(behavior => behavior.Given((bus, ctx) =>
                {
                    if (ctx.HasNativePubSubSupport)
                    {
                        ctx.Subscriber2Subscribed = true;
                    }
                }))
                .WithEndpoint<Publisher>(behavior => behavior
                        .CustomConfig(cfg => cfg.OnEndpointSubscribed(sub =>
                            {
                                if (sub.SubscriberReturnAddress.Queue.Contains("Subscriber1"))
                                {
                                    context.Subscriber1Subscribed = true;
                                }
                                else if (sub.SubscriberReturnAddress.Queue.Contains("Subscriber2"))
                                {
                                    context.Subscriber2Subscribed = true;
                                }
                                else
                                {
                                    throw new Exception($"Unknown subscriber subscribed to publisher: {sub.SubscriberReturnAddress}");
                                }
                            })
                        ).When(
                            ctx => ctx.Subscriber1Subscribed && ctx.Subscriber2Subscribed,
                            bus => bus.Publish<SampleEvent>()
                        )
                ).Done(ctx => TryGetMany("/api/errors", out failedMessages) && failedMessages.Count > 1)
                .Run();

            var subscriber1FailedMessage = failedMessages.SingleOrDefault(msg => msg.ReceivingEndpoint.Name.Contains("Subscriber1"));
            var subscriber2FailedMessage = failedMessages.SingleOrDefault(msg => msg.ReceivingEndpoint.Name.Contains("Subscriber2"));

            Assert.IsNotNull(subscriber1FailedMessage, "Subscriber1 did not report failed message");
            Assert.IsNotNull(subscriber2FailedMessage, "Subscriber2 did not report failed message");
            Assert.AreNotSame(subscriber1FailedMessage, subscriber2FailedMessage, "There should be two distinct failed messages");
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }
        }

        public class FailingSubscriber1 : EndpointConfigurationBuilder
        {
            public FailingSubscriber1()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 0;
                    }).AddMapping<SampleEvent>(typeof(Publisher));
            }

            public class SampleEventHandler : IHandleMessages<SampleEvent>
            {
                public void Handle(SampleEvent message)
                {
                    throw new Exception("Failing Subscriber 1");
                }
            }
        }

        public class FailingSubscriber2 : EndpointConfigurationBuilder
        {
            public FailingSubscriber2()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                    {
                        c.DisableFeature<SecondLevelRetries>();
                    })
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 0;
                    }).AddMapping<SampleEvent>(typeof(Publisher));
            }

            public class SampleEventHandler : IHandleMessages<SampleEvent>
            {
                public void Handle(SampleEvent message)
                {
                    throw new Exception("Failing Subscriber 2");
                }
            }
        }

        [Serializable]
        public class SampleEvent : IEvent
        {
        }

        public class FailingEventContext : ScenarioContext
        {
            public bool Subscriber1Subscribed { get; set; }
            public bool Subscriber2Subscribed { get; set; }
        }
    }
}
