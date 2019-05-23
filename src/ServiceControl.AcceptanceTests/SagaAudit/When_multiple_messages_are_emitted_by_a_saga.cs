namespace ServiceControl.AcceptanceTests.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.SagaAudit;

    class When_multiple_messages_are_emitted_by_a_saga : AcceptanceTest
    {
        [Test]
        public async Task Should_capture_all_outgoing_message_intents()
        {
            SagaHistory sagaHistory = null;

            var context = await Define<MyContext>()
                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga {Id = "Id"})))
                .Done(async c =>
                {
                    if (!c.SagaId.HasValue)
                    {
                        return false;
                    }

                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}");
                    sagaHistory = result;
                    return c.Done && result;
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
                    // NOTE: The default template disables this feature but that means the event will not be subscribed to or published
                    c.EnableFeature<AutoSubscribe>();
                    c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME);
                }, metadata => { metadata.RegisterPublisherFor<MessagePublishedBySaga>(typeof(SagaEndpoint)); });
            }

            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public async Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    await context.Reply(new MessageReplyBySaga { SagaId = Data.Id })
                        .ConfigureAwait(false);
                    await ReplyToOriginator(context, new MessageReplyToOriginatorBySaga { SagaId = Data.Id })
                        .ConfigureAwait(false);
                    await context.SendLocal(new MessageSentBySaga { SagaId = Data.Id })
                        .ConfigureAwait(false);
                    await context.Publish(new MessagePublishedBySaga { SagaId = Data.Id })
                        .ConfigureAwait(false);
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

            class MessageReplyBySagaHandler : IHandleMessages<MessageReplyBySaga>
            {
                public MyContext Context { get; set; }

                public Task Handle(MessageReplyBySaga message, IMessageHandlerContext context)
                {
                    if (!Context.SagaId.HasValue)
                    {
                        Context.SagaId = message.SagaId;
                    }

                    Context.Replied = true;
                    return Task.FromResult(0);
                }
            }

            class MessagePublishedBySagaHandler : IHandleMessages<MessagePublishedBySaga>
            {
                public MyContext Context { get; set; }

                public Task Handle(MessagePublishedBySaga message, IMessageHandlerContext context)
                {
                    if (!Context.SagaId.HasValue)
                    {
                        Context.SagaId = message.SagaId;
                    }

                    Context.Published = true;
                    return Task.FromResult(0);
                }
            }

            class MessageReplyToOriginatorBySagaHandler : IHandleMessages<MessageReplyToOriginatorBySaga>
            {
                public MyContext Context { get; set; }

                public Task Handle(MessageReplyToOriginatorBySaga message, IMessageHandlerContext context)
                {
                    if (!Context.SagaId.HasValue)
                    {
                        Context.SagaId = message.SagaId;
                    }

                    Context.RepliedToOriginator = true;
                    return Task.FromResult(0);
                }
            }

            class MessageSentBySagaHandler : IHandleMessages<MessageSentBySaga>
            {
                public MyContext Context { get; set; }

                public Task Handle(MessageSentBySaga message, IMessageHandlerContext context)
                {
                    if (!Context.SagaId.HasValue)
                    {
                        Context.SagaId = message.SagaId;
                    }

                    Context.Sent = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MessageInitiatingSaga : ICommand
        {
            public string Id { get; set; }
        }

        public class MessageSentBySaga : ICommand
        {
            public Guid SagaId { get; set; }
        }

        public class MessagePublishedBySaga : IEvent
        {
            public Guid SagaId { get; set; }
        }

        public class MessageReplyBySaga : IMessage
        {
            public Guid SagaId { get; set; }
        }

        public class MessageReplyToOriginatorBySaga : IMessage
        {
            public Guid SagaId { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public bool Sent { get; set; }
            public bool Replied { get; set; }
            public bool RepliedToOriginator { get; set; }
            public bool Published { get; set; }

            public bool Done => Sent && Replied && RepliedToOriginator && Published;
            public Guid? SagaId { get; set; }
        }
    }
}