namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using Raven.Client;
    using ServiceControl.Contracts;
    using ServiceControl.ExternalIntegrations;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Plugin.CustomChecks.Messages;

    /// <summary>
    /// The test simulates the heartbeat subsystem by publishing EndpointFailedToHeartbeat event.
    /// </summary>
    [TestFixture]
    public class When_encountered_an_error : AcceptanceTest
    {
        [Test]
        public async Task Dispatched_thread_is_restarted()
        {
            CustomConfiguration = config =>
            {
                config.OnEndpointSubscribed<MyContext>((s, ctx) =>
                {
                    if (s.SubscriberReturnAddress.IndexOf("ExternalProcessor", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ctx.Subscribed = true;
                    }
                });

                config.RegisterComponents(cc => cc.ConfigureComponent<FaultyPublisher>(DependencyLifecycle.SingleInstance));
            };
            
            var context = await Define<MyContext>()
                .WithEndpoint<ExternalProcessor>(b => b.When(async (messageSession, c) =>
                {
                    await messageSession.Subscribe<CustomCheckSucceeded>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.Subscribed = true;
                    }
                }).When(c => c.Subscribed, async session =>
                    {
                        var options = new SendOptions();
                        options.SetDestination(Settings.DEFAULT_SERVICE_NAME);
                        await session.Send(new ReportCustomCheckResult
                        {
                            EndpointName = "Testing",
                            HostId = Guid.NewGuid(),
                            Category = "Testing",
                            CustomCheckId = "Success custom check",
                            Host = "UnluckyHost",
                            ReportedAt = DateTime.Now

                        }, options).ConfigureAwait(false);
                    }))
                .Done(c => c.NotificationDelivered)
                .Run();

            Assert.IsTrue(context.NotificationDelivered);
            Assert.IsTrue(context.Failed);
        }

        private class FaultyPublisher : IEventPublisher
        {
            public MyContext Context { get; set; }

            public bool Handles(IDomainEvent @event)
            {
                return false;
            }

            public object CreateDispatchContext(IDomainEvent @event)
            {
                return null;
            }

            public Task<IEnumerable<object>> PublishEventsForOwnContexts(IEnumerable<object> allContexts, IAsyncDocumentSession session)
            {
                if (!failed)
                {
                    failed = true;
                    Context.Failed = true;
                    throw new Exception("Simulated exception");
                }

                return Task.FromResult(Enumerable.Empty<object>());
            }

            bool failed;
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<JsonServer>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MessageFailed).Assembly, Settings.DEFAULT_SERVICE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<CustomCheckSucceeded>(Settings.DEFAULT_SERVICE_NAME); });
            }

            public class EventHandler : IHandleMessages<CustomCheckSucceeded>
            {
                public MyContext Context { get; set; }

                public Task Handle(CustomCheckSucceeded message, IMessageHandlerContext context)
                {
                    Context.NotificationDelivered = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public bool NotificationDelivered { get; set; }
            public bool Failed { get; set; }
            public bool Subscribed { get; set; }
        }
    }
}