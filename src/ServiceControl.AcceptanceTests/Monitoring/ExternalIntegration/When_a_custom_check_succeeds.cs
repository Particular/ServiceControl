namespace ServiceControl.AcceptanceTests.Monitoring.ExternalIntegration
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Contracts;
    using Contracts.Operations;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestSupport.EndpointTemplates;

    [TestFixture]
    class When_a_custom_check_succeeds : AcceptanceTest
    {
        [Test]
        public async Task Should_publish_notification()
        {
            var externalProcessorSubscribed = false;
            CustomConfiguration = config => config.OnEndpointSubscribed<MyContext>((s, ctx) =>
            {
                if (s.SubscriberReturnAddress.IndexOf("ExternalProcessor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    externalProcessorSubscribed = true;
                }
            });

            ExecuteWhen(() => externalProcessorSubscribed, async domainEvents =>
            {
                await domainEvents.Raise(new Contracts.CustomChecks.CustomCheckSucceeded
                {
                    Category = "Testing",
                    CustomCheckId = "Success custom check",
                    OriginatingEndpoint = new EndpointDetails
                    {
                        Host = "MyHost",
                        HostId = Guid.Empty,
                        Name = "Testing"
                    },
                    SucceededAt = DateTime.Now
                });
            });

            var context = await Define<MyContext>()
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<CustomCheckSucceeded>();

                    if (c.HasNativePubSubSupport)
                    {
                        externalProcessorSubscribed = true;
                    }
                }))
                .Done(c => c.CustomCheckSucceededReceived)
                .Run();

            Assert.IsTrue(context.CustomCheckSucceededReceived);
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MessageFailed).Assembly, Settings.DEFAULT_SERVICE_NAME);
                }, publisherMetadata =>
                {
                    publisherMetadata.RegisterPublisherFor<CustomCheckSucceeded>(Settings.DEFAULT_SERVICE_NAME);
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
        }

        public class MyContext : ScenarioContext
        {
            public bool CustomCheckSucceededReceived { get; set; }
        }
    }
}