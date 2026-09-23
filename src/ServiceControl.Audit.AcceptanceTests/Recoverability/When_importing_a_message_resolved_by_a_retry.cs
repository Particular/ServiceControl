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
    using Contracts.EndpointControl;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_importing_a_message_resolved_by_a_retry : AcceptanceTest
    {
        [Test]
        public async Task Should_set_status_to_resolved()
        {
            SetSettings = settings => settings.ServiceControlQueueAddress = Conventions.EndpointNamingConvention(typeof(ServiceControlSpy));

            MessagesView auditedMessage = null;

            var messageId = Guid.NewGuid().ToString();
            var context = await Define<Context>()
                // The audit instance probes the primary queue on startup with an empty message the spy cannot deserialize.
                .WithEndpoint<ServiceControlSpy>(b => b.DoNotFailOnErrorMessages())
                .WithEndpoint<Receiver>(b => b.When(s =>
                {
                    var options = new SendOptions();

                    options.SetHeader("ServiceControl.Retry.UniqueMessageId", "CAN BE ANYTHING");
                    options.RouteToThisEndpoint();
                    options.SetMessageId(messageId);
                    return s.Send(new MyMessage(), options);
                }))
                .Done(async c =>
                {
                    var receiverRegistered = c.SentRegisterEndpointCommands.Any(command => command.Endpoint.Name == Conventions.EndpointNamingConvention(typeof(Receiver)));
                    if (!receiverRegistered)
                    {
                        return false;
                    }

                    var result = await this.TryGetSingle<MessagesView>("/api/messages", m => m.MessageId == messageId);

                    auditedMessage = result;

                    return result && receiverRegistered;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(auditedMessage.Status, Is.EqualTo(MessageStatus.ResolvedSuccessfully));
                Assert.That(context.SentRegisterEndpointCommands, Has.Some.Matches<RegisterNewEndpoint>(command => command.Endpoint.Name == Conventions.EndpointNamingConvention(typeof(Receiver))));
            }
        }

        public class Context : ScenarioContext
        {
            public ConcurrentBag<RegisterNewEndpoint> SentRegisterEndpointCommands { get; } = [];
        }

        public class ServiceControlSpy : EndpointConfigurationBuilder
        {
            public ServiceControlSpy() => EndpointSetup<DefaultServerWithoutAudit>();

            [Handler]
            public class RegisterNewEndpointHandler(Context testContext) : IHandleMessages<RegisterNewEndpoint>
            {
                public Task Handle(RegisterNewEndpoint message, IMessageHandlerContext context)
                {
                    testContext.SentRegisterEndpointCommands.Add(message);
                    return Task.CompletedTask;
                }
            }
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

        public class MyMessage : ICommand;
    }
}