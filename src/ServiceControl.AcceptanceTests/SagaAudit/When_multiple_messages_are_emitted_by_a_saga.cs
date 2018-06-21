namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.SagaAudit;

    public class When_multiple_messages_are_emitted_by_a_saga : AcceptanceTest
    {
        [Test]
        public async Task All_outgoing_message_intents_should_be_captured()
        {
            SagaHistory sagaHistory = null;

            var context = await Define<MyContext>()
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga { Id = "Id" })))
                .Done(async c =>
                {
                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}");
                    sagaHistory = result;
                    return c.Done && result;
                })
                .Run(TimeSpan.FromSeconds(40));

            Assert.NotNull(sagaHistory);

            Assert.AreEqual(context.SagaId, sagaHistory.SagaId);
            Assert.AreEqual(typeof(EndpointThatIsHostingTheSaga.MySaga).FullName, sagaHistory.SagaType);

            var sagaStateChange = sagaHistory.Changes.First();
            Assert.AreEqual("Send", sagaStateChange.InitiatingMessage.Intent);

            var outgoingIntents = new Dictionary<string, string>();
            foreach (var message in sagaStateChange.OutgoingMessages)
            {
                outgoingIntents[message.MessageType] = message.Intent;
            }

            // TODO: These first two were originally send before the V5-7 update. Needs to be checked
            Assert.AreEqual("Reply", outgoingIntents[typeof(MessageReplyBySaga).FullName]);
            Assert.AreEqual("Reply", outgoingIntents[typeof(MessageReplyToOriginatorBySaga).FullName]);
            Assert.AreEqual("Send", outgoingIntents[typeof(MessageSentBySaga).FullName]);
            Assert.AreEqual("Publish", outgoingIntents[typeof(MessagePublishedBySaga).FullName]);
        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    // NOTE: The default template disables this feature but that means the event will not be subscribed to or published
                    c.EnableFeature<AutoSubscribe>();
                    c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME);
                    
                }, metadata =>
                {
                    metadata.RegisterPublisherFor<MessagePublishedBySaga>(typeof(EndpointThatIsHostingTheSaga));
                });
            }

            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public MyContext Context { get; set; }

                public async Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    Context.SagaId = Data.Id;

                    await context.Reply(new MessageReplyBySaga())
                        .ConfigureAwait(false);
                    await ReplyToOriginator(context, new MessageReplyToOriginatorBySaga())
                        .ConfigureAwait(false);
                    await context.SendLocal(new MessageSentBySaga())
                        .ConfigureAwait(false);
                    await context.Publish(new MessagePublishedBySaga())
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
                    Context.Done1 = true;
                    return Task.FromResult(0);
                }
            }

            class MessagePublishedBySagaHandler : IHandleMessages<MessagePublishedBySaga>
            {
                public MyContext Context { get; set; }

                public Task Handle(MessagePublishedBySaga message, IMessageHandlerContext context)
                {
                    Context.Done2 = true;
                    return Task.FromResult(0);
                }
            }

            class MessageReplyToOriginatorBySagaHandler : IHandleMessages<MessageReplyToOriginatorBySaga>
            {
                public MyContext Context { get; set; }

                public Task Handle(MessageReplyToOriginatorBySaga message, IMessageHandlerContext context)
                {
                    Context.Done3 = true;
                    return Task.FromResult(0);
                }
            }

            class MessageSentBySagaHandler : IHandleMessages<MessageSentBySaga>
            {
                public MyContext Context { get; set; }

                public Task Handle(MessageSentBySaga message, IMessageHandlerContext context)
                {
                    Context.Done4 = true;
                    return Task.FromResult(0);
                }
            }
        }

        [Serializable]
        public class MessageInitiatingSaga : ICommand
        {
            public string Id { get; set; }
        }

        [Serializable]
        public class MessageSentBySaga : ICommand
        {
        }

        [Serializable]
        public class MessagePublishedBySaga : IEvent
        {
        }

        [Serializable]
        public class MessageReplyBySaga : IMessage
        {
        }

        [Serializable]
        public class MessageReplyToOriginatorBySaga : IMessage
        {
        }

        public class MyContext : ScenarioContext
        {
            public bool Done1 { get; set; }
            public bool Done2 { get; set; }
            public bool Done3 { get; set; }
            public bool Done4 { get; set; }

            public bool Done => Done1 && Done2 && Done3 && Done4;
            public Guid SagaId { get; set; }
        }
    }

}