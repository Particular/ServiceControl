﻿namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Saga;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.ProductionDebugging.Api;

    class When_a_message_hitting_multiple_sagas_is_audited : AcceptanceTest
    {
        [Test]
        public void Saga_info_should_be_available_through_the_http_api()
        {
            var context = new MyContext();
            MessagesView auditedMessage = null;
           

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.Given((bus, c) => bus.SendLocal(new MessageInitiatingSaga())))
                .Done(c =>
                {
                    if (c.SagaId == Guid.Empty)
                    {
                        return false;
                    }

                    return TryGetSingle("/api/messages", out auditedMessage, m => m.MessageId == c.MessageId);
                })
                .Run(TimeSpan.FromSeconds(40));

        
            Assert.NotNull(auditedMessage);

            Assert.AreEqual(typeof(EndpointThatIsHostingTheSaga.MySaga).FullName, auditedMessage.InvokedSagas.First().SagaType);
            Assert.AreEqual(typeof(EndpointThatIsHostingTheSaga.MyOtherSaga).FullName, auditedMessage.InvokedSagas.Last().SagaType);
            
            Assert.AreEqual(context.SagaId, auditedMessage.InvokedSagas.First().SagaId);
            Assert.AreEqual(context.OtherSagaId, auditedMessage.InvokedSagas.Last().SagaId);

            Assert.AreEqual("New", auditedMessage.InvokedSagas.First().ChangeStatus);
            Assert.AreEqual("Completed", auditedMessage.InvokedSagas.Last().ChangeStatus);
        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }

            public class MySaga:Saga<MySaga.MySagaData>,IAmStartedByMessages<MessageInitiatingSaga>
            {
                public MyContext Context { get; set; }
                
                public void Handle(MessageInitiatingSaga message)
                {
                    Context.SagaId = Data.Id;
                    Context.MessageId = Bus.CurrentMessageContext.Id;
                }

                public class MySagaData : ContainSagaData
                {
                }
            }

            public class MyOtherSaga : Saga<MyOtherSaga.MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public MyContext Context { get; set; }

                public void Handle(MessageInitiatingSaga message)
                {
                    Context.OtherSagaId = Data.Id;
                    Context.MessageId = Bus.CurrentMessageContext.Id;

                    MarkAsComplete();
                }

                public class MySagaData : ContainSagaData
                {
                }
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
            public Guid OtherSagaId { get; set; }
        }
    }
}
