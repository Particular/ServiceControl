namespace ServiceControl.Audit.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NUnit.Framework;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_message_hitting_multiple_sagas_is_audited : AcceptanceTest
    {
        [Test]
        public async Task Saga_info_should_be_available_through_the_http_api()
        {
            MessagesView auditedMessage = null;

            var context = await Define<MyContext>()
                .WithEndpoint<SagaAuditProcessorFake>()
                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga { Id = "Id" })))
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
                .Run();

            Assert.That(auditedMessage, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(auditedMessage.InvokedSagas.First().SagaType, Is.EqualTo(typeof(SagaEndpoint.MySaga).FullName));
                Assert.That(auditedMessage.InvokedSagas.Last().SagaType, Is.EqualTo(typeof(SagaEndpoint.MyOtherSaga).FullName));

                Assert.That(auditedMessage.InvokedSagas.First().SagaId, Is.EqualTo(context.SagaId));
                Assert.That(auditedMessage.InvokedSagas.Last().SagaId, Is.EqualTo(context.OtherSagaId));

                Assert.That(auditedMessage.InvokedSagas.First().ChangeStatus, Is.EqualTo("New"));
                Assert.That(auditedMessage.InvokedSagas.Last().ChangeStatus, Is.EqualTo("Completed"));
            });
        }

        public class SagaAuditProcessorFake : EndpointConfigurationBuilder
        {
            public SagaAuditProcessorFake() => EndpointSetup<DefaultServerWithoutAudit>(c => c.Pipeline.Register(new IgnoreAllBehavior(), "Ignore all messages"));

            class IgnoreAllBehavior : Behavior<ITransportReceiveContext>
            {
                public override Task Invoke(ITransportReceiveContext context, Func<Task> next) => Task.CompletedTask;
            }
        }

        public class SagaEndpoint : EndpointConfigurationBuilder
        {
            public SagaEndpoint() =>
                //we need to enable the plugin for it to enrich the audited messages, state changes will go to input queue and just be discarded
                EndpointSetup<DefaultServerWithAudit>(c => c.AuditSagaStateChanges(Conventions.EndpointNamingConvention(typeof(SagaAuditProcessorFake))));

            public class MySaga(MyContext testContext)
                : Saga<MySaga.MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    testContext.SagaId = Data.Id;
                    testContext.MessageId = context.MessageId;
                    return Task.CompletedTask;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                    => mapper.MapSaga(saga => saga.MessageId).ToMessage<MessageInitiatingSaga>(msg => msg.Id);

                public class MySagaData : ContainSagaData
                {
                    public string MessageId { get; set; }
                }
            }

            public class MyOtherSaga(MyContext testContext)
                : Saga<MyOtherSaga.MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    testContext.OtherSagaId = Data.Id;
                    testContext.MessageId = context.MessageId;

                    MarkAsComplete();

                    return Task.CompletedTask;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper) =>
                    mapper.MapSaga(saga => saga.MessageId).ToMessage<MessageInitiatingSaga>(msg => msg.Id);

                public class MySagaData : ContainSagaData
                {
                    public string MessageId { get; set; }
                }
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
            public Guid OtherSagaId { get; set; }
        }
    }
}