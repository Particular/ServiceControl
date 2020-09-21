namespace ServiceControl.Audit.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;
    using TestSupport.EndpointTemplates;

    [RunOnAllTransports]
    class When_a_saga_instance_is_being_created : AcceptanceTest
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

        [Test]
        public async Task Saga_information_should_be_removed_after_retention_period()
        {
            SetSettings = settings =>
            {
                settings.ExpirationProcessTimerInSeconds = 1;
                settings.AuditRetentionPeriod = TimeSpan.FromSeconds(10);
            };

            var context = await Define<MyContext>()
                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new StartSagaMessage { Id = "Id" })))
                .Do("Ensure SagaHistory created", async c =>
                {
                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}");
                    return c.InitiatingMessageReceived && result;
                })
                .Do("Ensure SagaHistory removed", async c =>
                {
                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}");
                    if (!result)
                    {
                        c.SagaHistoryRemoved = true;
                        return true;
                    }
                    return false;
                })
                .Done(c => c.SagaHistoryRemoved)
                .Run();

            Assert.IsTrue(context.SagaHistoryRemoved);
        }

        public class SagaEndpoint : EndpointConfigurationBuilder
        {
            public SagaEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(
                    c => c.AuditSagaStateChanges("audit"));
            }
        }

        public class MySaga : Saga<MySagaData>,
            IAmStartedByMessages<StartSagaMessage>
        {
            public MyContext Context { get; set; }

            public Task Handle(StartSagaMessage message, IMessageHandlerContext context)
            {
                Context.SagaId = Data.Id.ToString();
                Context.InitiatingMessageReceived = true;
                return Task.FromResult(0);
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
            {
                mapper.ConfigureMapping<StartSagaMessage>(msg => msg.Id).ToSaga(saga => saga.MessageId);
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

        public class MyContext : ScenarioContext, ISequenceContext
        {
            public string MessageId { get; set; }
            public string SagaId { get; set; }
            public bool InitiatingMessageReceived { get; set; }
            public bool SagaHistoryRemoved { get; set; }
            public int Step { get; set; }
        }
    }
}