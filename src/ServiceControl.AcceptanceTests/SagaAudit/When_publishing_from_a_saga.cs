namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using System.Reflection;
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

            Define(context)
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.Given((bus, c) => bus.SendLocal(new StartSagaMessage())))
                .Done(c => 
                    c.ReceivedInitiatingMessage && 
                  //  c.ReceivedEvent && 
                    TryGet("/api/sagas/" + c.SagaId, out sagaHistory))
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
                EndpointSetup<DefaultServerWithAudit>(c => c.DisableFeature<AutoSubscribe>())
                    .AddMapping<MyEvent>(typeof(EndpointThatIsHostingTheSaga))
                    .IncludeAssembly(Assembly.LoadFrom("ServiceControl.Plugin.Nsb5.SagaAudit.dll"));
            }
        }

        public class MySaga : Saga<MySagaData>,
            IAmStartedByMessages<StartSagaMessage>,
            IHandleMessages<MyEvent>
        {
            public MyContext Context { get; set; }
            public void Handle(StartSagaMessage message)
            {
                Context.SagaId = Data.Id;
                Context.ReceivedInitiatingMessage = true;
                Bus.Publish(new MyEvent());
            }

            public void Handle(MyEvent message)
            {
                Context.ReceivedEvent = true;
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