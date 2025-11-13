namespace ServiceControl.AcceptanceTests.Monitoring.ExternalIntegration
{
    using System;
    using System.Collections.Generic;
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

            ExecuteWhen(() => externalProcessorSubscribed, async domainEvents =>
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
                    FailedAt = DateTime.UtcNow,
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

            Assert.That(context.CustomCheckFailedReceived, Is.True);

            var enclosedType = context.IntegrationEventHeaders[Headers.EnclosedMessageTypes];
            Assert.That(enclosedType, Is.EqualTo("ServiceControl.Contracts.CustomCheckFailed, ServiceControl.Contracts"));
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
                    publisherMetadata.RegisterPublisherFor<CustomCheckFailed>(PrimaryOptions.DEFAULT_INSTANCE_NAME);
                });

            public class CustomCheckFailedHandler(MyContext testContext) : IHandleMessages<CustomCheckFailed>
            {
                public Task Handle(CustomCheckFailed message, IMessageHandlerContext context)
                {
                    testContext.CustomCheckFailedReceived = true;
                    testContext.IntegrationEventHeaders = context.MessageHeaders;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public bool CustomCheckFailedReceived { get; set; }
            public IReadOnlyDictionary<string, string> IntegrationEventHeaders { get; set; }
        }
    }
}