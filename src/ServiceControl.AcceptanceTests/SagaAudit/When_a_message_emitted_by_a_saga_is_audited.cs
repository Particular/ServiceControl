namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Saga;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
  
    public class When_a_message_emitted_by_a_saga_is_audited : AcceptanceTest
    {

        [Test]
        public void Info_on_emitted_saga_should_be_available_through_the_http_api()
        {
            var context = new MyContext();
            MessagesView auditedMessage = null;
           

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.Given((bus, c) => bus.SendLocal(new MessageInitiatingSaga())))
                .Done(c => TryGetSingle("/api/messages", out auditedMessage,m => m.MessageId == c.MessageId))
                .Run(TimeSpan.FromSeconds(40));

        
            Assert.NotNull(auditedMessage.OriginatesFromSaga);

            Assert.AreEqual(typeof(EndpointThatIsHostingTheSaga.MySaga).FullName, auditedMessage.OriginatesFromSaga.SagaType);
            Assert.AreEqual(context.SagaId, auditedMessage.OriginatesFromSaga.SagaId);

        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }

            public class MySaga:Saga<MySagaData>,IAmStartedByMessages<MessageInitiatingSaga>
            {
                public MyContext Context { get; set; }

                
                public void Handle(MessageInitiatingSaga message)
                {
                    Context.SagaId = Data.Id;

                    Bus.SendLocal(new MessageSentBySaga());
                }
            }

            public class MySagaData : ContainSagaData
            {
            }

            class MessageSentBySagaHandler : IHandleMessages<MessageSentBySaga>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }
                public void Handle(MessageSentBySaga message)
                {
                    Context.MessageId = Bus.CurrentMessageContext.Id;
                }
            }
        }

        [Serializable]
        public class MessageInitiatingSaga : ICommand
        {
        }

        [Serializable]
        public class MessageSentBySaga : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public Guid SagaId { get; set; }
        }
    }

    
}