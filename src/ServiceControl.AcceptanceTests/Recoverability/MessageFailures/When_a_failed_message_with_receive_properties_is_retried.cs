namespace ServiceControl.AcceptanceTests.RavenDB.Recoverability.MessageFailures
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;

    class When_a_failed_message_with_receive_properties_is_retried : AcceptanceTest
    {
        [Test]
        public async Task Should_preserve_receive_dispatch_properties()
        {
            //HINT: LearningTransport defines one receive property "LearningTransport.FileCreatedAt"
            //      The property should have the same value when initially processed as when retried
            var scenarioContext = await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    FailedMessage failedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    if (failedMessage == null)
                    {
                        return false;
                    }

                    ctx.FailureGroupId = failedMessage.FailureGroups.First().Id;

                    return true;
                }, async (bus, ctx) =>
                {
                    ctx.AboutToSendRetry = true;
                    await this.Post<object>($"/api/recoverability/groups/{ctx.FailureGroupId}/errors/retry");
                }).DoNotFailOnErrorMessages())
                .Done(ctx => ctx.Retried)
                .Run();

            Assert.That(scenarioContext.SecondAttemptFileCreatedAt, Is.EqualTo(scenarioContext.FirstAttemptFileCreatedAt), "Receive properties are not preserved");
        }

        public class FailingEndpoint : EndpointConfigurationBuilder
        {
            public FailingEndpoint() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.GetSettings().Get<TransportDefinition>().TransportTransactionMode =
                        TransportTransactionMode.ReceiveOnly;
                    c.EnableFeature<Outbox>();

                    c.RegisterStartupTask(new SendMessageAtStart());

                    c.NoRetries();
                });

            class SendMessageAtStart : FeatureStartupTask
            {
                protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
                    => session.SendLocal(new MyMessage(), cancellationToken);

                protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
                    => Task.CompletedTask;
            }

            [Handler]
            public class MyMessageHandler(Context scenarioContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Message Handled");
                    if (scenarioContext.AboutToSendRetry)
                    {
                        scenarioContext.SecondAttemptFileCreatedAt = context.Extensions.Get<IncomingMessage>().ReceiveProperties["LearningTransport.FileCreatedAt"];
                        scenarioContext.Retried = true;
                    }
                    else
                    {
                        scenarioContext.FirstAttemptFileCreatedAt = context.Extensions.Get<IncomingMessage>().ReceiveProperties["LearningTransport.FileCreatedAt"];
                        scenarioContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, settings.EndpointName()).ToString();
                        throw new Exception("Simulated Exception");
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class Context : ScenarioContext
        {
            public string FirstAttemptFileCreatedAt { get; set; }
            public string SecondAttemptFileCreatedAt { get; set; }
            public string UniqueMessageId { get; set; }
            public string FailureGroupId { get; set; }
            public bool Retried { get; set; }
            public bool AboutToSendRetry { get; set; }
        }

        public class MyMessage : ICommand;
    }
}