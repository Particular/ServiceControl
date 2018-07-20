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

    public class When_requesting_timeout_from_a_saga : AcceptanceTest
    {
        [Test]
        public async Task Saga_audit_trail_should_contain_the_state_change()
        {
            SagaHistory sagaHistory = null;

            var context = await Define<MyContext>()
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.When((bus, c) => bus.SendLocal(new StartSagaMessage { Id = "Id" })))
                .Done(async c =>
                {
                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}", sh => sh.Changes.Any(change => change.Status == SagaStateChangeStatus.Updated));
                    sagaHistory = result;
                    return c.ReceivedTimeoutMessage && result;
                })
                .Run();

            Assert.NotNull(sagaHistory);

            Assert.AreEqual(context.SagaId, sagaHistory.SagaId);
            Assert.AreEqual(typeof(MySaga).FullName, sagaHistory.SagaType);

            var updateChange = sagaHistory.Changes.Single(x => x.Status == SagaStateChangeStatus.Updated);
            Assert.AreEqual(typeof(TimeoutMessage).FullName, updateChange.InitiatingMessage.MessageType);
        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME));
            }

        }
        public class MySaga : Saga<MySagaData>,
            IAmStartedByMessages<StartSagaMessage>,
            IHandleTimeouts<TimeoutMessage>
        {
            public Task Handle(StartSagaMessage message, IMessageHandlerContext context)
            {
                Context.SagaId = Data.Id;
                return RequestTimeout<TimeoutMessage>(context, TimeSpan.FromMilliseconds(10));
            }

            public Task Timeout(TimeoutMessage stat, IMessageHandlerContext context)
            {
                Context.ReceivedTimeoutMessage = true;
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

        public class TimeoutMessage
        {
        }

        public class StartSagaMessage : ICommand
        {
            public string Id { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public Guid SagaId { get; set; }
            public bool ReceivedTimeoutMessage { get; set; }
        }
    }
}