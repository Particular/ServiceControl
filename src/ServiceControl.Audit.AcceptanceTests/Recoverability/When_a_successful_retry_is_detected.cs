namespace ServiceControl.Audit.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    [RunOnAllTransports]
    class When_a_successful_retry_is_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_send_acknowledgement()
        {
            var failedMessageId = Guid.NewGuid().ToString();
            var context = await Define<Context>()
                .WithEndpoint<Receiver>(b => b.When(s =>
                {
                    var options = new SendOptions();

                    options.SetHeader("ServiceControl.Retry.UniqueMessageId", failedMessageId);
                    options.SetHeader("ServiceControl.Retry.AcknowledgementQueue", Conventions.EndpointNamingConvention(typeof(AcknowledgementSpy)));
                    options.RouteToThisEndpoint();
                    return s.Send(new MyMessage(), options);
                }))
                .WithEndpoint<AcknowledgementSpy>()
                .Done(c => c.AcknowledgementSent)
                .Run();

            Assert.IsTrue(context.AcknowledgementSent);
        }

        public class Context : ScenarioContext
        {
            public bool AcknowledgementSent { get; set; }
        }

        public class AcknowledgementSpy : EndpointConfigurationBuilder
        {
            public AcknowledgementSpy()
            {
                EndpointSetup<DefaultServerWithoutAudit>(cfg =>
                {
                    cfg.Pipeline.Register(b => new SpyBehavior(b.Build<Context>()), "Spy behavior");
                });
            }

            public class SpyBehavior : Behavior<ITransportReceiveContext>
            {
                Context scenarioContext;

                public SpyBehavior(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
                {
                    if (context.Message.Headers.ContainsKey("ServiceControl.Retry.Successful"))
                    {
                        scenarioContext.AcknowledgementSent = true;
                    }
                    return Task.CompletedTask;
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : ICommand
        {
        }
    }
}
