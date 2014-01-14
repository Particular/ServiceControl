namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Saga;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;

    public class When_publishing_from_a_saga : AcceptanceTest
    {

        [Test]
        public void Saga_audit_trail_should_contain_the_state_change()
        {
            var context = new MyContext();
            SagaHistory sagaHistory = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.Given((bus, c) => bus.SendLocal(new MessageInitiatingSaga())))
                .Done(c => 
                    c.ReceivedInitiatingMessage && 
                  //  c.ReceivedEvent && 
                    TryGet("/api/sagas/" + c.SagaId, out sagaHistory))
                .Run(TimeSpan.FromSeconds(5));

            Assert.NotNull(sagaHistory);

            Assert.AreEqual(context.SagaId, sagaHistory.SagaId);
            Assert.AreEqual(typeof(EndpointThatIsHostingTheSaga.MySaga).FullName,sagaHistory.SagaType);

            var newChange = sagaHistory.Changes.Single(x => x.Status == SagaStateChangeStatus.New);
            Assert.AreEqual(typeof(MessageInitiatingSaga).FullName, newChange.InitiatingMessage.MessageType);

        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServer>(c => Configure.Features.Disable<AutoSubscribe>())
                    .AuditTo(Address.Parse("audit"))
                    .AddMapping<MyEvent>(typeof(EndpointThatIsHostingTheSaga));
            }

            public class MySaga : Saga<MySagaData>, 
                IAmStartedByMessages<MessageInitiatingSaga>,
                IHandleMessages<MyEvent>
            {
                public MyContext Context { get; set; }
                public void Handle(MessageInitiatingSaga message)
                {
                    Context.SagaId = Data.Id;
                    Context.ReceivedInitiatingMessage = true;
                    Bus.Publish(new MyEvent());
                }

                public void Handle(MyEvent message)
                {
                    Context.ReceivedEvent = true;
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
            public bool ReceivedInitiatingMessage { get; set; }
            public bool ReceivedEvent { get; set; }
            public Guid SagaId { get; set; }
        }
        public class MyEvent:IEvent
        {
        }
    }

}