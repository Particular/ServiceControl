﻿namespace ServiceControl.Audit.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_message_hitting_a_saga_is_audited : AcceptanceTest
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


            Assert.NotNull(auditedMessage);

            Assert.AreEqual(typeof(SagaEndpoint.MySaga).FullName, auditedMessage.InvokedSagas.Single().SagaType);
            Assert.AreEqual(context.SagaId, auditedMessage.InvokedSagas.First().SagaId);
            Assert.AreEqual("New", auditedMessage.InvokedSagas.First().ChangeStatus);
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

            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                MyContext testContext;

                public MySaga(MyContext testContext) => this.testContext = testContext;

                public Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    testContext.SagaId = Data.Id;
                    testContext.MessageId = context.MessageId;
                    return Task.CompletedTask;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper) => mapper.ConfigureMapping<MessageInitiatingSaga>(msg => msg.Id).ToSaga(saga => saga.MessageId);
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