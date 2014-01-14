namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Saga;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;

    public class When_starting_a_saga_instance_being_created : AcceptanceTest
    {

        [Test]
        public void Saga_audit_trail_should_contain_the_state_change()
        {
            var context = new MyContext();
            SagaHistory sagaHistory = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.Given((bus, c) => bus.SendLocal(new MessageInitiatingSaga())))
                .Done(c => c.InitiatingMessageReceived  && TryGet("/api/sagas/" + c.SagaId, out sagaHistory))
                .Run(TimeSpan.FromSeconds(5));

            Assert.NotNull(sagaHistory);

            Assert.AreEqual(context.SagaId, sagaHistory.SagaId);
            Assert.AreEqual(typeof(EndpointThatIsHostingTheSaga.MySaga).FullName,sagaHistory.SagaType);

            var change = sagaHistory.Changes.Single();
            
            Assert.AreEqual(SagaStateChangeStatus.New, change.Status);
            Assert.AreEqual(typeof(MessageInitiatingSaga).FullName, change.InitiatingMessage.MessageType);
        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }

            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public MyContext Context { get; set; }
                public void Handle(MessageInitiatingSaga message)
                {
                    Context.SagaId = Data.Id;
                    Context.InitiatingMessageReceived = true;
                }
            }

            public class MySagaData : ContainSagaData
            {
            }
        }

        [Serializable]
        public class MessageInitiatingSaga : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public Guid SagaId { get; set; }
            public bool InitiatingMessageReceived { get; set; }
        }
    }

    
}