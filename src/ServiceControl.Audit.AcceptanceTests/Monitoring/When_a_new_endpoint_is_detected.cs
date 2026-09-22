namespace ServiceControl.Audit.AcceptanceTests.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NUnit.Framework;
    using Contracts.EndpointControl;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_new_endpoint_is_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_notify_service_control()
        {
            SetSettings = s => s.ServiceControlQueueAddress = Conventions.EndpointNamingConvention(typeof(SpyEndpoint));

            var context = await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .WithEndpoint<SpyEndpoint>()
                .Done(c => c.ReceivedRegisterEndpointCommands.Any())
                .Run();

            var command = context.ReceivedRegisterEndpointCommands.Single();
            Assert.That(command.Endpoint.Name, Is.EqualTo(Conventions.EndpointNamingConvention(typeof(Receiver))));
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
