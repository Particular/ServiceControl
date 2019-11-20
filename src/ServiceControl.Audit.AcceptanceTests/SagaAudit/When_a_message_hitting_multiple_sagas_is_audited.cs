﻿namespace ServiceControl.Audit.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.Auditing.MessagesView;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;

    class When_a_message_hitting_multiple_sagas_is_audited : AcceptanceTest
    {
        [Test]
        public async Task Saga_info_should_be_available_through_the_http_api()
        {
            MessagesView auditedMessage = null;

            var context = await Define<MyContext>()
                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga {Id = "Id"})))
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

            Assert.NotNull(auditedMessage);

            Assert.AreEqual(typeof(SagaEndpoint.MySaga).FullName, auditedMessage.InvokedSagas.First().SagaType);
            Assert.AreEqual(typeof(SagaEndpoint.MyOtherSaga).FullName, auditedMessage.InvokedSagas.Last().SagaType);

            Assert.AreEqual(context.SagaId, auditedMessage.InvokedSagas.First().SagaId);
            Assert.AreEqual(context.OtherSagaId, auditedMessage.InvokedSagas.Last().SagaId);

            Assert.AreEqual("New", auditedMessage.InvokedSagas.First().ChangeStatus);
            Assert.AreEqual("Completed", auditedMessage.InvokedSagas.Last().ChangeStatus);
        }

        public class SagaEndpoint : EndpointConfigurationBuilder
        {
            public SagaEndpoint()
            {
                //we need to enable the plugin for it to enrich the audited messages, state changes will go to our input queue and just be discarded
                EndpointSetup<DefaultServerWithAudit>(c => c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME));
            }

            public class MySaga : Saga<MySaga.MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
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

                public class MySagaData : ContainSagaData
                {
                    public string MessageId { get; set; }
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

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
                    mapper.ConfigureMapping<MessageInitiatingSaga>(msg => msg.Id).ToSaga(saga => saga.MessageId);
                }

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