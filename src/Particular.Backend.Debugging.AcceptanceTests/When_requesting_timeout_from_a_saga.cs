namespace Particular.Backend.Debugging.AcceptanceTests
{
    using System;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Saga;
    using NUnit.Framework;
    using Particular.Backend.Debugging.AcceptanceTests.Contexts;
    using Particular.Backend.Debugging.Api;

    public class When_requesting_timeout_from_a_saga : AcceptanceTest
    {

        [Test]
        public void Saga_audit_trail_should_contain_the_state_change()
        {
            var context = new MyContext();
            SagaHistory sagaHistory = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.Given((bus, c) => bus.SendLocal(new StartSagaMessage())))
                .Done(c =>
                {
                    var url = "/api/sagas/" + c.SagaId;
                    return c.ReceivedTimeoutMessage && 
                                      TryGet(url, out sagaHistory,
                                          sh=>sh.Changes.Any(change=>change.Status == SagaStateChangeStatus.Updated));
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
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }

        }
        public class MySaga : Saga<MySagaData>,
            IAmStartedByMessages<StartSagaMessage>,
            IHandleTimeouts<TimeoutMessage>
        {
            public MyContext Context { get; set; }

            public void Handle(StartSagaMessage message)
            {
                Context.SagaId = Data.Id;
                RequestTimeout<TimeoutMessage>(TimeSpan.FromMilliseconds(10));
            }

            public void Timeout(TimeoutMessage state)
            {
                Context.ReceivedTimeoutMessage = true;
            }
        }

        public class MySagaData : ContainSagaData
        {
        }

        public class TimeoutMessage
        {
        }

        public class StartSagaMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public Guid SagaId { get; set; }
            public bool ReceivedTimeoutMessage { get; set; }
        }
    }

}