namespace ServiceControl.AcceptanceTests.Monitoring.ExternalIntegration
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Contracts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations;

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
                    SucceededAt = DateTime.UtcNow
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

            Assert.That(context.CustomCheckSucceededReceived, Is.True);
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MessageFailed).Assembly, PrimaryOptions.DEFAULT_INSTANCE_NAME);
                }, publisherMetadata =>
                {
                    publisherMetadata.RegisterPublisherFor<CustomCheckSucceeded>(PrimaryOptions.DEFAULT_INSTANCE_NAME);
                });

            public class CustomCheckSucceededHandler(MyContext testContext) : IHandleMessages<CustomCheckSucceeded>
            {
                public Task Handle(CustomCheckSucceeded message, IMessageHandlerContext context)
                {
                    testContext.CustomCheckSucceededReceived = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public bool CustomCheckSucceededReceived { get; set; }
        }
    }
}