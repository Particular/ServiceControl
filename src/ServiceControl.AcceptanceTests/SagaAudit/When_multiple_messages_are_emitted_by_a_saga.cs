namespace ServiceControl.AcceptanceTests.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.SagaAudit;
    using TestSupport.EndpointTemplates;

    class When_multiple_messages_are_emitted_by_a_saga : AcceptanceTest
    {
        [Test]
        public async Task Should_capture_all_outgoing_message_intents()
        {
            SagaHistory sagaHistory = null;

            var context = await Define<MyContext>()
                .WithEndpoint<SagaEndpoint>(b => b.When(async (bus, c) =>
                {
                    await bus.SendLocal(new MessageInitiatingSaga { Id = "Id" });
                }))
                .Done(async c =>
                {
                    if (!c.SagaId.HasValue)
                    {
                        return false;
                    }

                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}");
                    sagaHistory = result;
                    return result;
                })
                .Run();

            Assert.NotNull(sagaHistory);

            Assert.AreEqual(context.SagaId, sagaHistory.SagaId);
            Assert.AreEqual(typeof(SagaEndpoint.MySaga).FullName, sagaHistory.SagaType);

            var sagaStateChange = sagaHistory.Changes.First();
            Assert.AreEqual("Send", sagaStateChange.InitiatingMessage.Intent);

            var outgoingIntents = new Dictionary<string, string>();
            foreach (var message in sagaStateChange.OutgoingMessages)
            {
                Trace.WriteLine($"{message.MessageType} - {message.Intent}");
                outgoingIntents[message.MessageType] = message.Intent;
            }

            Assert.AreEqual("Reply", outgoingIntents[typeof(MessageReplyBySaga).FullName], "MessageReplyBySaga was not present");
            Assert.AreEqual("Reply", outgoingIntents[typeof(MessageReplyToOriginatorBySaga).FullName], "MessageReplyToOriginatorBySaga was not present");
            Assert.AreEqual("Send", outgoingIntents[typeof(MessageSentBySaga).FullName], "MessageSentBySaga was not present");
            Assert.AreEqual("Publish", outgoingIntents[typeof(MessagePublishedBySaga).FullName], "MessagePublishedBySaga was not present");
        }


        public class SagaEndpoint : EndpointConfigurationBuilder
        {
            public SagaEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME);
                });
            }

            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                readonly MyContext testContext;

                public MySaga(MyContext testContext)
                {
                    this.testContext = testContext;
                }

                public async Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    await context.Reply(new MessageReplyBySaga())
                        .ConfigureAwait(false);
                    await ReplyToOriginator(context, new MessageReplyToOriginatorBySaga())
                        .ConfigureAwait(false);
                    await context.SendLocal(new MessageSentBySaga())
                        .ConfigureAwait(false);
                    await context.Publish(new MessagePublishedBySaga())
                        .ConfigureAwait(false);
                    testContext.SagaId = Data.Id;
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

            public class NopHandler : IHandleMessages<MessageReplyBySaga>,
                IHandleMessages<MessageReplyToOriginatorBySaga>,
                IHandleMessages<MessageSentBySaga>,
                IHandleMessages<MessagePublishedBySaga>
            {
                public Task Handle(MessagePublishedBySaga message, IMessageHandlerContext context)
                    => Task.CompletedTask;

                public Task Handle(MessageReplyBySaga message, IMessageHandlerContext context)
                    => Task.CompletedTask;

                public Task Handle(MessageReplyToOriginatorBySaga message, IMessageHandlerContext context)
                    => Task.CompletedTask;

                public Task Handle(MessageSentBySaga message, IMessageHandlerContext context)
                    => Task.CompletedTask;
            }
        }

        public class MessageInitiatingSaga : ICommand
        {
            public string Id { get; set; }
        }

        public class MessageSentBySaga : ICommand
        {
        }

        public class MessagePublishedBySaga : IEvent
        {
        }

        public class MessageReplyBySaga : IMessage
        {
        }

        public class MessageReplyToOriginatorBySaga : IMessage
        {
        }

        public class MyContext : ScenarioContext
        {
            public Guid? SagaId { get; set; }
        }
    }
}