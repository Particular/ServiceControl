namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;

    public class When_a_message_hitting_a_saga_is_audited : AcceptanceTest
    {
        [Test]
        public async Task Saga_info_should_be_available_through_the_http_api()
        {
            MessagesView auditedMessage = null;

            var context = await Define<MyContext>()
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga { Id = "Id" })))
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

            Assert.AreEqual(typeof(EndpointThatIsHostingTheSaga.MySaga).FullName, auditedMessage.InvokedSagas.Single().SagaType);
            Assert.AreEqual(context.SagaId, auditedMessage.InvokedSagas.First().SagaId);
            Assert.AreEqual("New", auditedMessage.InvokedSagas.First().ChangeStatus);
        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME));
            }

            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public MyContext Context { get; set; }

                public Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    Context.SagaId = Data.Id;
                    Context.MessageId = context.MessageId;
                    return Task.FromResult(0);
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
                    mapper.ConfigureMapping<MessageInitiatingSaga>(msg => msg.Id).ToSaga(saga => saga.MessageId);
                }
            }

            public class MySagaData : ContainSagaData
            {
                public string MessageId { get; set; }
            }
        }

        public class MessageInitiatingSaga : ICommand
        {
            public string Id { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public Guid SagaId { get; set; }
        }
    }
}