namespace ServiceControl.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.SagaAudit;

    class When_publishing_from_a_saga : AcceptanceTest
    {
        [Test]
        public async Task Saga_audit_trail_should_contain_the_state_change()
        {
            SagaHistory sagaHistory = null;

            var context = await Define<MyContext>()
                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new StartSagaMessage {Id = "Id"})))
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
            Assert.AreEqual(typeof(MySaga).FullName, sagaHistory.SagaType);

            var newChange = sagaHistory.Changes.Single(x => x.Status == SagaStateChangeStatus.New);
            Assert.AreEqual(typeof(StartSagaMessage).FullName, newChange.InitiatingMessage.MessageType);
        }

        public class SagaEndpoint : EndpointConfigurationBuilder
        {
            public SagaEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                    {
                        // NOTE: The DefaultServerWithoutAudit disables this
                        c.EnableFeature<AutoSubscribe>();
                        c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME);
                    },
                    metadata => { metadata.RegisterPublisherFor<MyEvent>(typeof(SagaEndpoint)); });
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
                return context.Publish(new MyEvent {Id = message.Id});
            }

            public Task Handle(MyEvent message, IMessageHandlerContext context)
            {
                Context.ReceivedEvent = true;

                return Task.FromResult(0);
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
            {
                mapper.ConfigureMapping<StartSagaMessage>(msg => msg.Id).ToSaga(saga => saga.MessageId);
                mapper.ConfigureMapping<MyEvent>(msg => msg.Id).ToSaga(saga => saga.MessageId);
            }
        }

        public class MySagaData : ContainSagaData
        {
            public string MessageId { get; set; }
        }

        public class StartSagaMessage : ICommand
        {
            public string Id { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public bool ReceivedInitiatingMessage { get; set; }
            public bool ReceivedEvent { get; set; }
            public Guid SagaId { get; set; }
        }

        public class MyEvent : IEvent
        {
            public string Id { get; set; }
        }
    }
}