namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using ServiceControl.Contracts;
    using ServiceControl.Plugin.CustomChecks.Messages;

    [TestFixture]
    public class When_a_message_has_custom_checks : AcceptanceTest
    {
        [Test]
        public async Task Notification_should_be_published_on_the_bus()
        {
            CustomConfiguration = config => config.OnEndpointSubscribed<MyContext>((s, ctx) =>
            {
                if (s.SubscriberReturnAddress.IndexOf("ExternalProcessor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (s.MessageType.Contains("CustomCheckSucceeded"))
                    {
                        ctx.SubscribedToCustomCheckSucceeded = true;
                    }
                    if (s.MessageType.Contains("CustomCheckFailed"))
                    {
                        ctx.SubscribedToCustomCheckFailed = true;
                    }
                }
            });

            var context = await Define<MyContext>()
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<CustomCheckSucceeded>();
                    await bus.Subscribe<CustomCheckFailed>();
                    if (c.HasNativePubSubSupport)
                    {
                        c.SubscribedToCustomCheckFailed = true;
                        c.SubscribedToCustomCheckSucceeded = true;
                    }
                }).When(c => c.SubscribedToCustomCheckFailed && c.SubscribedToCustomCheckSucceeded, async session =>
                {
                    var options = new SendOptions();
                    options.SetDestination(Settings.DEFAULT_SERVICE_NAME);
                    await session.Send(new ReportCustomCheckResult
                    {
                        EndpointName = "Testing",
                        HostId = Guid.NewGuid(),
                        Category = "Testing",
                        CustomCheckId = "Success custom check",
                        Host = "MyHost",
                        ReportedAt = DateTime.Now

                    }, options).ConfigureAwait(false);

                    options = new SendOptions();
                    options.SetDestination(Settings.DEFAULT_SERVICE_NAME);
                    await session.Send(new ReportCustomCheckResult
                    {
                        EndpointName = "Testing",
                        HostId = Guid.NewGuid(),
                        Category = "Testing",
                        CustomCheckId = "Fail custom check",
                        Host = "MyHost",
                        FailureReason = "Because I can",
                        HasFailed = true,
                        ReportedAt = DateTime.Now

                    }, options).ConfigureAwait(false);
                }))
                .Done(c => c.CustomCheckFailedReceived && c.CustomCheckSucceededReceived)
                .Run();

            Assert.IsTrue(context.CustomCheckFailedReceived);
            Assert.IsTrue(context.CustomCheckSucceededReceived);
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<JsonServer>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MessageFailed).Assembly, Settings.DEFAULT_SERVICE_NAME);
                }, publisherMetadata =>
                {
                    publisherMetadata.RegisterPublisherFor<CustomCheckSucceeded>(Settings.DEFAULT_SERVICE_NAME);
                    publisherMetadata.RegisterPublisherFor<CustomCheckFailed>(Settings.DEFAULT_SERVICE_NAME);
                });
            }

            public class CustomCheckSucceededHandler : IHandleMessages<CustomCheckSucceeded>
            {
                public MyContext Context { get; set; }

                public Task Handle(CustomCheckSucceeded message, IMessageHandlerContext context)
                {
                    Context.CustomCheckSucceededReceived = true;
                    return Task.FromResult(0);
                }
            }

            public class CustomCheckFailedHandler : IHandleMessages<CustomCheckFailed>
            {
                public MyContext Context { get; set; }

                public Task Handle(CustomCheckFailed message, IMessageHandlerContext context)
                {
                    Context.CustomCheckFailedReceived = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public bool CustomCheckSucceededReceived { get; set; }
            public bool CustomCheckFailedReceived { get; set; }
            public bool SubscribedToCustomCheckSucceeded { get; set; }
            public bool SubscribedToCustomCheckFailed { get; set; }
        }
    }
}