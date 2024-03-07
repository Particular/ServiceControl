namespace ServiceControl.MultiInstance.AcceptanceTests.Auditing
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using CompositeViews.Messages;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using TestSupport;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    // HINT: If this is included inside of the test class, the learning transport cannot set up the subscriptions
    class SomeEvent : IEvent { }

    class When_event_processed_by_multiple_endpoints : AcceptanceTest
    {
        [Test]
        public async Task Should_find_both_occurrences()
        {
            CustomPrimaryEndpointConfiguration = config => config.OnEndpointSubscribed<MyContext>(
                (subscription, context) =>
                {
                    context.Subscriber1Subscribed = subscription.SubscriberEndpoint ==
                                                    Conventions.EndpointNamingConvention(typeof(Subscriber1));
                    context.Subscriber2Subscribed = subscription.SubscriberEndpoint ==
                                                    Conventions.EndpointNamingConvention(typeof(Subscriber2));
                }
            );

            await Define<MyContext>()
                .WithEndpoint<Subscriber1>(b => b.When(async (session, ctx) =>
                {
                    await session.Subscribe<SomeEvent>();

                    if (ctx.HasNativePubSubSupport)
                    {
                        ctx.Subscriber1Subscribed = true;
                    }
                }))
                .WithEndpoint<Subscriber2>(b => b.When(async (session, ctx) =>
                {
                    await session.Subscribe<SomeEvent>();

                    if (ctx.HasNativePubSubSupport)
                    {
                        ctx.Subscriber2Subscribed = true;
                    }
                }))
                .WithEndpoint<Publisher>(b => b.When(
                    ctx => ctx.Subscriber1Subscribed && ctx.Subscriber2Subscribed,
                    session => session.Publish(new SomeEvent())))
                .Done(async c => c.MessageId != null && (await this.TryGetMany<MessagesView>("/api/messages")).Items.Count == 2)
                .Run();
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }
        }

        class Subscriber1 : EndpointConfigurationBuilder
        {
            public Subscriber1()
            {
                EndpointSetup<DefaultServerWithAudit>(
                    _ => { },
                    publisherMetaData => publisherMetaData.RegisterPublisherFor<SomeEvent>(typeof(Publisher))
                );
            }

            class SomeEventHandler : IHandleMessages<SomeEvent>
            {
                readonly MyContext testContext;
                readonly IReadOnlySettings settings;

                public SomeEventHandler(MyContext testContext, IReadOnlySettings settings)
                {
                    this.testContext = testContext;
                    this.settings = settings;
                }

                public Task Handle(SomeEvent message, IMessageHandlerContext context)
                {
                    testContext.Subscriber1Endpoint = settings.EndpointName();
                    testContext.MessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }

        class Subscriber2 : EndpointConfigurationBuilder
        {
            public Subscriber2()
            {
                EndpointSetup<DefaultServerWithAudit>(
                    _ => { },
                    publisherMetaData => publisherMetaData.RegisterPublisherFor<SomeEvent>(typeof(Publisher))
                );
            }

            class SomeEventHandler : IHandleMessages<SomeEvent>
            {
                readonly MyContext scenarioContext;
                readonly IReadOnlySettings settings;

                public SomeEventHandler(MyContext scenarioContext, IReadOnlySettings settings)
                {
                    this.scenarioContext = scenarioContext;
                    this.settings = settings;
                }

                public Task Handle(SomeEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.Subscriber2Endpoint = settings.EndpointName();
                    return Task.CompletedTask;
                }
            }
        }

        class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public bool Subscriber1Subscribed { get; set; }
            public string Subscriber1Endpoint { get; set; }
            public bool Subscriber2Subscribed { get; set; }
            public string Subscriber2Endpoint { get; set; }
        }
    }
}