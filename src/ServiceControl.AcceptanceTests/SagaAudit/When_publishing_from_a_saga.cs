namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.SagaAudit;

    public class When_publishing_from_a_saga : AcceptanceTest
    {
        [Test]
        public async Task Saga_audit_trail_should_contain_the_state_change()
        {
            SagaHistory sagaHistory = null;

            var context = await Define<MyContext>()
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.When((bus, c) => bus.SendLocal(new StartSagaMessage())))
                .Done(async c =>
                {
                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}");
                    sagaHistory = result;
                    return c.ReceivedInitiatingMessage &&
                           result;
                })
                .Run();

            Assert.NotNull(sagaHistory);

            Assert.AreEqual(context.SagaId, sagaHistory.SagaId);
            Assert.AreEqual(typeof(MySaga).FullName,sagaHistory.SagaType);

            var newChange = sagaHistory.Changes.Single(x => x.Status == SagaStateChangeStatus.New);
            Assert.AreEqual(typeof(StartSagaMessage).FullName, newChange.InitiatingMessage.MessageType);
        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                    {
                        c.DisableFeature<AutoSubscribe>();
                        c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME);

                        var routing = c.ConfigureTransport().Routing();
                        routing.RouteToEndpoint(typeof(MyEvent), typeof(EndpointThatIsHostingTheSaga));
                    });
            }
        }

        public class MySaga : Saga<MySagaData>,
            IAmStartedByMessages<StartSagaMessage>,
            IHandleMessages<MyEvent>
        {
            public MyContext Context { get; set; }
            public Task Handle(StartSagaMessage message, IMessageHandlerContext context)
            {
                Context.SagaId = Data.Id;
                Context.ReceivedInitiatingMessage = true;
                return context.Publish(new MyEvent());
            }

            public Task Handle(MyEvent message, IMessageHandlerContext context)
            {
                Context.ReceivedEvent = true;

                return Task.FromResult(0);
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
            {
            }
        }

        public class MySagaData : ContainSagaData
        {
        }

        public class StartSagaMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public bool ReceivedInitiatingMessage { get; set; }
            public bool ReceivedEvent { get; set; }
            public Guid SagaId { get; set; }
        }

        public class MyEvent:IEvent
        {
        }
    }

}