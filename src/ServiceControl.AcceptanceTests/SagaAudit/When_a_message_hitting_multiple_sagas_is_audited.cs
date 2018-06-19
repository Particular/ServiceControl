namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;

    class When_a_message_hitting_multiple_sagas_is_audited : AcceptanceTest
    {
        [Test]
        public async Task Saga_info_should_be_available_through_the_http_api()
        {
            MessagesView auditedMessage = null;

            var context = await Define<MyContext>()
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga())))
                .Done(async c =>
                {
                    if (c.SagaId == Guid.Empty)
                    {
                        return false;
                    }

                    var result = await this.TryGetSingle<MessagesView>("/api/messages", m => m.MessageId == c.MessageId);
                    auditedMessage = result;
                    return result;
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
                EndpointSetup<DefaultServerWithAudit>(c => c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME));
            }

            public class MySaga:Saga<MySaga.MySagaData>,IAmStartedByMessages<MessageInitiatingSaga>
            {
                public MyContext Context { get; set; }

                public Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    Context.SagaId = Data.Id;
                    Context.MessageId = context.MessageId;
                    return Task.FromResult(0);
                }

                public class MySagaData : ContainSagaData
                {
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
                }
            }

            public class MyOtherSaga : Saga<MyOtherSaga.MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public MyContext Context { get; set; }

                public Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    Context.OtherSagaId = Data.Id;
                    Context.MessageId = context.MessageId;

                    MarkAsComplete();

                    return Task.FromResult(0);
                }

                public class MySagaData : ContainSagaData
                {
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
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
