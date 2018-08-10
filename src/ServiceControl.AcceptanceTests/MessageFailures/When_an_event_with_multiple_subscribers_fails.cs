namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;

    class When_an_event_with_multiple_subscribers_fails : AcceptanceTest
    {
        [Test]
        public async Task There_should_be_a_FailedMessage_for_each_subscriber()
        {
            var failedMessages = new List<FailedMessageView>();

            await Define<FailingEventContext>()
                .WithEndpoint<FailingSubscriber1>(behavior => behavior.When(async (bus, ctx) =>
                {
                    await bus.Subscribe<SampleEvent>();

                    if (ctx.HasNativePubSubSupport)
                    {
                        ctx.Subscriber1Subscribed = true;
                    }
                }).DoNotFailOnErrorMessages())
                .WithEndpoint<FailingSubscriber2>(behavior => behavior.When(async (bus, ctx) =>
                {
                    await bus.Subscribe<SampleEvent>();

                    if (ctx.HasNativePubSubSupport)
                    {
                        ctx.Subscriber2Subscribed = true;
                    }
                }).DoNotFailOnErrorMessages())
                .WithEndpoint<Publisher>(behavior => behavior
                    .CustomConfig(cfg => cfg.OnEndpointSubscribed<FailingEventContext>((s, ctx) =>
                        {
                            if (s.SubscriberReturnAddress.IndexOf("Subscriber1", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                ctx.Subscriber1Subscribed = true;
                            }
                            else if (s.SubscriberReturnAddress.IndexOf("Subscriber2", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                ctx.Subscriber2Subscribed = true;
                            }
                            else
                            {
                                throw new Exception($"Unknown subscriber subscribed to publisher: {s.SubscriberReturnAddress}");
                            }
                        })
                    ).When(
                        ctx => ctx.Subscriber1Subscribed && ctx.Subscriber2Subscribed,
                        bus => bus.Publish<SampleEvent>()
                    ).DoNotFailOnErrorMessages()
                ).Done(async ctx =>
                {
                    var result = await this.TryGetMany<FailedMessageView>("/errors");
                    failedMessages = result;
                    return result && failedMessages.Sum(x => x.NumberOfProcessingAttempts) >= 2;
                })
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
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var recoverability = c.Recoverability();
                    recoverability.Immediate(s => s.NumberOfRetries(0));
                    recoverability.Delayed(s => s.NumberOfRetries(0));

                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(SampleEvent), typeof(Publisher));
                }, metadata => metadata.RegisterPublisherFor<SampleEvent>(typeof(Publisher)));
            }

            public class SampleEventHandler : IHandleMessages<SampleEvent>
            {
                public Task Handle(SampleEvent message, IMessageHandlerContext context)
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
                    var recoverability = c.Recoverability();
                    recoverability.Immediate(s => s.NumberOfRetries(0));
                    recoverability.Delayed(s => s.NumberOfRetries(0));

                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(SampleEvent), typeof(Publisher));
                }, metadata => metadata.RegisterPublisherFor<SampleEvent>(typeof(Publisher)));
            }

            public class SampleEventHandler : IHandleMessages<SampleEvent>
            {
                public Task Handle(SampleEvent message, IMessageHandlerContext context)
                {
                    throw new Exception("Failing Subscriber 2");
                }
            }
        }


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