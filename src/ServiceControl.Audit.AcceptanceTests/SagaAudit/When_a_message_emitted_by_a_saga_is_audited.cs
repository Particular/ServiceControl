﻿namespace ServiceControl.Audit.AcceptanceTests.SagaAudit
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    class When_a_message_emitted_by_a_saga_is_audited : AcceptanceTest
    {
        [Test]
        public async Task Info_on_emitted_saga_should_be_available_through_the_http_api()
        {
            MessagesView auditedMessage = null;

            var context = await Define<MyContext>()
                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga { Id = "Id" })))
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<MessagesView>("/api/messages", m => m.MessageId == c.MessageId);
                    auditedMessage = result;
                    return result;
                })
                .Run();

            Assert.That(auditedMessage.OriginatesFromSaga, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(auditedMessage.OriginatesFromSaga.SagaType, Is.EqualTo(typeof(SagaEndpoint.MySaga).FullName));
                Assert.That(auditedMessage.OriginatesFromSaga.SagaId, Is.EqualTo(context.SagaId));
            });
        }

        public class SagaEndpoint : EndpointConfigurationBuilder
        {
            public SagaEndpoint() => EndpointSetup<DefaultServerWithAudit>();

            public class MySaga(MyContext testContext) : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    testContext.SagaId = Data.Id;

                    return context.SendLocal(new MessageSentBySaga());
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

            class MessageSentBySagaHandler(MyContext testContext) : IHandleMessages<MessageSentBySaga>
            {
                public Task Handle(MessageSentBySaga message, IMessageHandlerContext context)
                {
                    testContext.MessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }

        public class MessageInitiatingSaga : ICommand
        {
            public string Id { get; set; }
        }

        public class MessageSentBySaga : ICommand;

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }
            public Guid SagaId { get; set; }
        }
    }
}