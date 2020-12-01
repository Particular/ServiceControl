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
    [RunOnAllTransports]
    class When_a_custom_check_fails : AcceptanceTest
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

            _ = ExecuteWhen(() => externalProcessorSubscribed, async domainEvents =>
            {
                await domainEvents.Raise(new Contracts.CustomChecks.CustomCheckFailed
                {
                    Category = "Testing",
                    CustomCheckId = "Fail custom check",
                    OriginatingEndpoint = new EndpointDetails
                    {
                        Host = "MyHost",
                        HostId = Guid.Empty,
                        Name = "Testing"
                    },
                    FailedAt = DateTime.Now,
                    FailureReason = "Because I can"
                });
            });

            var context = await Define<MyContext>()
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<CustomCheckFailed>();

                    if (c.HasNativePubSubSupport)
                    {
                        externalProcessorSubscribed = true;
                    }
                }))
                .Done(c => c.CustomCheckFailedReceived)
                .Run();

            Assert.IsTrue(context.CustomCheckFailedReceived);
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
                    publisherMetadata.RegisterPublisherFor<CustomCheckFailed>(Settings.DEFAULT_SERVICE_NAME);
                });
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
            public bool CustomCheckFailedReceived { get; set; }
        }
    }
}