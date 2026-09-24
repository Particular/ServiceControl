namespace ServiceControl.Audit.AcceptanceTests.Recoverability
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing.MessagesView;
    using Audit.Monitoring;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NUnit.Framework;
    using Contracts.EndpointControl;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_importing_a_message_resolved_by_a_retry : AcceptanceTest
    {
        [Test]
        public async Task Should_set_status_to_resolved()
        {
            SetSettings = s => s.ServiceControlQueueAddress = Conventions.EndpointNamingConvention(typeof(SpyEndpoint));

            MessagesView auditedMessage = null;

            var messageId = Guid.NewGuid().ToString();
            var context = await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(s =>
                {
                    var options = new SendOptions();

                    options.SetHeader("ServiceControl.Retry.UniqueMessageId", "CAN BE ANYTHING");
                    options.RouteToThisEndpoint();
                    options.SetMessageId(messageId);
                    return s.Send(new MyMessage(), options);
                }))
                .WithEndpoint<SpyEndpoint>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<MessagesView>("/api/messages", m => m.MessageId == messageId);

                    auditedMessage = result;

                    return result.HasResult && c.ReceivedRegisterEndpointCommands.Any();
                })
                .Run();

            Assert.That(auditedMessage.Status, Is.EqualTo(MessageStatus.ResolvedSuccessfully));
            Assert.That(context.ReceivedRegisterEndpointCommands, Is.Not.Empty);
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithAudit>();

            [Handler]
            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context) => Task.CompletedTask;
            }
        }

        public class SpyEndpoint : EndpointConfigurationBuilder
        {
            public SpyEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c =>
                c.Pipeline.Register(new IgnoreCustomCheckResultsBehavior(), "Ignores custom check results reported to the spied queue"));

            class IgnoreCustomCheckResultsBehavior : Behavior<ITransportReceiveContext>
            {
                public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
                {
                    if (context.Message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes)
                        && messageTypes.StartsWith(CustomCheckResultType, StringComparison.Ordinal))
                    {
                        return Task.CompletedTask;
                    }

                    return next();
                }

                const string CustomCheckResultType = "ServiceControl.Plugin.CustomChecks.Messages.ReportCustomCheckResult";
            }

            [Handler]
            public class RegisterNewEndpointHandler(MyContext testContext) : IHandleMessages<RegisterNewEndpoint>
            {
                public Task Handle(RegisterNewEndpoint message, IMessageHandlerContext context)
                {
                    testContext.ReceivedRegisterEndpointCommands.Add(message);
                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : ICommand;

        public class MyContext : ScenarioContext
        {
            public ConcurrentBag<RegisterNewEndpoint> ReceivedRegisterEndpointCommands { get; } = [];
        }
    }
}
