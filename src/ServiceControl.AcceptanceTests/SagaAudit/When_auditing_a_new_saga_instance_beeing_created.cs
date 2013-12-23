namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Saga;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;

    public class When_auditing_a_new_saga_instance_beeing_created : AcceptanceTest
    {

        [Test]
        public void Saga_audit_trail_should_contain_the_state_change()
        {
            var context = new MyContext();
            SagaHistory sagaHistory = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.Given((bus, c) => bus.SendLocal(new MessageInitatingSaga())))
                .Done(c =>
                {
                    if (c.SagaId == Guid.Empty)
                    {
                        return false;
                    }

                    return TryGet("/api/sagas/" + c.SagaId, out sagaHistory);
                })
                .Run(TimeSpan.FromSeconds(40));

            Assert.NotNull(sagaHistory);
        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }

            class MySaga:Saga<MySagaData>,IAmStartedByMessages<MessageInitatingSaga>
            {
                public MyContext Context { get; set; }
                public void Handle(MessageInitatingSaga message)
                {
                    Context.SagaId = Data.Id;
                }
            }

            class MySagaData : ContainSagaData
            {
            }
        }

        [Serializable]
        public class MessageInitatingSaga : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public Guid SagaId { get; set; }
        }
    }

    
}