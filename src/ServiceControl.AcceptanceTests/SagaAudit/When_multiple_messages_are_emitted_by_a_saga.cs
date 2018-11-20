namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
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
                EndpointSetup<DefaultServerWithAudit>(c =>
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
                    Context.Done1();
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
                    Context.Done2();
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
                    Context.Done3();
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
                    Context.Done4();
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
            long steps;

            public void Done1()
            {
                Interlocked.Increment(ref steps);
            }

            public void Done2()
            {
                Interlocked.Increment(ref steps);
            }

            public void Done3()
            {
                Interlocked.Increment(ref steps);
            }

            public void Done4()
            {
                Interlocked.Increment(ref steps);
            }

            public bool Done => Interlocked.Read(ref steps) >= 4;
            public Guid? SagaId { get; set; }
        }
    }
}