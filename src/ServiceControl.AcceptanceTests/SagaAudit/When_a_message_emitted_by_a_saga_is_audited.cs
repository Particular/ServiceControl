﻿namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Threading.Tasks;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Saga;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    public class When_a_message_emitted_by_a_saga_is_audited : AcceptanceTest
    {

        [Test]
        public async Task Info_on_emitted_saga_should_be_available_through_the_http_api()
        {
            var context = new MyContext();
            MessagesView auditedMessage = null;

            await Define(context)
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.Given((bus, c) => bus.SendLocal(new MessageInitiatingSaga())))
                .Done(async c =>
                {
                    var result = await TryGetSingle<MessagesView>("/api/messages", m => m.MessageId == c.MessageId);
                    auditedMessage = result;
                    return result;
                })
                .Run(TimeSpan.FromSeconds(40));

            Assert.NotNull(auditedMessage.OriginatesFromSaga);

            Assert.AreEqual(typeof(EndpointThatIsHostingTheSaga.MySaga).FullName, auditedMessage.OriginatesFromSaga.SagaType);
            Assert.AreEqual(context.SagaId, auditedMessage.OriginatesFromSaga.SagaId);
        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public MyContext Context { get; set; }

                public void Handle(MessageInitiatingSaga message)
                {
                    Context.SagaId = Data.Id;

                    Bus.SendLocal(new MessageSentBySaga());
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
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