namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using ServiceControl.SagaAudit;

    public class When_a_saga_instance_is_being_created : AcceptanceTest
    {
        [Test]
        public async Task Saga_audit_trail_should_contain_the_state_change()
        {
            SagaHistory sagaHistory = null;

            var context = await Define<MyContext>()
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.When((bus, c) => bus.SendLocal(new StartSagaMessage { Id = "Id" })))
                .Done(async c =>
                {
                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}");
                    sagaHistory = result;
                    return c.InitiatingMessageReceived && result;
                })
                .Run();

            Assert.NotNull(sagaHistory);

            Assert.AreEqual(context.SagaId, sagaHistory.SagaId);
            Assert.AreEqual(typeof(MySaga).FullName, sagaHistory.SagaType);

            var change = sagaHistory.Changes.Single();

            Assert.AreEqual(SagaStateChangeStatus.New, change.Status);
            Assert.AreEqual(typeof(StartSagaMessage).FullName, change.InitiatingMessage.MessageType);
        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServerWithAudit>(
                        c => c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME));
            }
        }

        public class MySaga : Saga<MySagaData>,
            IAmStartedByMessages<StartSagaMessage>
        {
            public Task Handle(StartSagaMessage message, IMessageHandlerContext context)
            {
                Context.SagaId = Data.Id;
                Context.InitiatingMessageReceived = true;
                return Task.FromResult(0);
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
            {
                mapper.ConfigureMapping<StartSagaMessage>(msg => msg.Id).ToSaga(saga => saga.MessageId);
            }

            public MyContext Context { get; set; }
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
            public string MessageId { get; set; }
            public Guid SagaId { get; set; }
            public bool InitiatingMessageReceived { get; set; }
        }
    }
}