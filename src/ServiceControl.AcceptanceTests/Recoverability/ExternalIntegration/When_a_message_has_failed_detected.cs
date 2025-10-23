namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Contracts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;

    class When_a_message_has_failed_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_publish_notification()
        {
            CustomConfiguration = config => config.OnEndpointSubscribed<MyContext>((s, ctx) =>
            {
                if (s.SubscriberReturnAddress.IndexOf("ExternalProcessor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ctx.ExternalProcessorSubscribed = true;
                }
            });

            var context = await Define<MyContext>()
                .WithEndpoint<FailingReceiver>(b => b.When(c => c.ExternalProcessorSubscribed, (bus, c) =>
                {
                    var options = new SendOptions();
                    options.SetHeader("AcceptanceTestRunId", c.TestRunId.ToString());
                    options.RouteToThisEndpoint();
                    return bus.Send(new MyMessage { Body = "Faulty message" }, options);
                }).DoNotFailOnErrorMessages())
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<MessageFailed>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                }))
                .Done(c => c.EventDelivered)
                .Run();

            var deserializedEvent = JsonSerializer.Deserialize<MessageFailed>(context.Event);

            Assert.Multiple(() =>
            {
                Assert.That(deserializedEvent.FailureDetails.Exception.Message, Is.EqualTo("Faulty message"));
                //These are important so check it they are set
                Assert.That(deserializedEvent.MessageDetails.MessageId, Is.Not.Null);
                Assert.That(deserializedEvent.SendingEndpoint.Name, Is.Not.Null);
                Assert.That(deserializedEvent.ProcessingEndpoint.Name, Is.Not.Null);
            });
        }

        public class FailingReceiver : EndpointConfigurationBuilder
        {
            public FailingReceiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => { c.Recoverability().Immediate(s => s.NumberOfRetries(2)).Delayed(s => s.NumberOfRetries(0)); });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context) => throw new Exception(message.Body);
            }
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MessageFailed).Assembly, PrimaryOptions.DEFAULT_INSTANCE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<MessageFailed>(PrimaryOptions.DEFAULT_INSTANCE_NAME); });

            public class FailureHandler(MyContext testContext) : IHandleMessages<MessageFailed>
            {
                public Task Handle(MessageFailed message, IMessageHandlerContext context)
                {
                    if (!message.MessageDetails.Headers.TryGetValue("AcceptanceTestRunId", out var runId) || runId != testContext.TestRunId.ToString())
                    {
                        return Task.CompletedTask;
                    }

                    var serializedMessage = JsonSerializer.Serialize(message);
                    testContext.Event = serializedMessage;
                    testContext.EventDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }


        public class MyMessage : ICommand
        {
            public string Body { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public bool ExternalProcessorSubscribed { get; set; }
            public string Event { get; set; }
            public bool EventDelivered { get; set; }
        }
    }
}