namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    class When_error_forwarding_is_enabled : AcceptanceTest
    {
        [Test]
        public async Task Should_forward_the_failed_message_to_the_error_log_queue()
        {
            SetSettings = settings =>
            {
                settings.ForwardErrorMessages = true;
                settings.ErrorLogQueue = NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Spy));
            };

            var context = await Define<MyContext>()
                .WithEndpoint<Failing>(b => b
                    .When(session => session.SendLocal(new ForwardedMessage()))
                    .DoNotFailOnErrorMessages())
                // The ingestor probes the forwarding address on startup with an empty message the
                // spy cannot deserialize, so the spy has to tolerate its own failures.
                .WithEndpoint<Spy>(b => b.DoNotFailOnErrorMessages())
                .Done(c => c.ForwardedMessageId != null)
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.ForwardedMessageId, Is.EqualTo(context.FailedMessageId));
                Assert.That(context.ForwardedWithFailureHeaders, Is.True);
            }
        }

        public class Failing : EndpointConfigurationBuilder
        {
            public Failing() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            [Handler]
            public class ForwardedMessageHandler(MyContext testContext) : IHandleMessages<ForwardedMessage>
            {
                public Task Handle(ForwardedMessage message, IMessageHandlerContext context)
                {
                    testContext.FailedMessageId = context.MessageId;
                    throw new Exception("Simulated exception");
                }
            }
        }

        public class Spy : EndpointConfigurationBuilder
        {
            public Spy() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            [Handler]
            public class ForwardedMessageHandler(MyContext testContext) : IHandleMessages<ForwardedMessage>
            {
                public Task Handle(ForwardedMessage message, IMessageHandlerContext context)
                {
                    testContext.ForwardedWithFailureHeaders = context.MessageHeaders.ContainsKey("NServiceBus.ExceptionInfo.Message");
                    testContext.ForwardedMessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public string FailedMessageId { get; set; }
            public string ForwardedMessageId { get; set; }
            public bool ForwardedWithFailureHeaders { get; set; }
        }
    }

    public class ForwardedMessage : ICommand;
}
