﻿namespace Particular.Backend.Debugging.AcceptanceTests
{
    using System;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Saga;
    using NUnit.Framework;
    using Particular.Backend.Debugging.AcceptanceTests.Contexts;
    using Particular.Backend.Debugging.Api;

    public class When_a_saga_instance_is_being_created : AcceptanceTest
    {

        [Test]
        public void Saga_audit_trail_should_contain_the_state_change()
        {
            var context = new MyContext();
            SagaHistory sagaHistory = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.Given((bus, c) => bus.SendLocal(new StartSagaMessage())))
                .Done(c => c.InitiatingMessageReceived  && TryGet("/api/sagas/" + c.SagaId, out sagaHistory))
                .Run();

            Assert.NotNull(sagaHistory);

            Assert.AreEqual(context.SagaId, sagaHistory.SagaId);
            Assert.AreEqual(typeof(MySaga).FullName,sagaHistory.SagaType);

            var change = sagaHistory.Changes.Single();

            Assert.AreEqual(SagaStateChangeStatus.New, change.Status);
            Assert.AreEqual(typeof(StartSagaMessage).FullName, change.InitiatingMessage.MessageType);
        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }

        }

        public class MySaga : Saga<MySagaData>,
            IAmStartedByMessages<StartSagaMessage>
        {
            public MyContext Context { get; set; }

            public void Handle(StartSagaMessage message)
            {
                Context.SagaId = Data.Id;
                Context.InitiatingMessageReceived = true;
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
            public string MessageId { get; set; }
            public Guid SagaId { get; set; }
            public bool InitiatingMessageReceived { get; set; }
        }
    }

    
}