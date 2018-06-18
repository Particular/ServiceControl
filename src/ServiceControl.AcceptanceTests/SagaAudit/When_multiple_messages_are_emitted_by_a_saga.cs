namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.SagaAudit;

    public class When_multiple_messages_are_emitted_by_a_saga : AcceptanceTest
    {
        [Test]
        public async Task All_outgoing_message_intents_should_be_captured()
        {
            SagaHistory sagaHistory = null;

            var context = await Define<MyContext>()
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga())))
                .Done(async c =>
                {
                    var result = await TryGet<SagaHistory>($"/api/sagas/{c.SagaId}");
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

            Assert.AreEqual("Send", outgoingIntents[typeof(MessageReplyBySaga).FullName]);
            Assert.AreEqual("Send", outgoingIntents[typeof(MessageReplyToOriginatorBySaga).FullName]);
            Assert.AreEqual("Send", outgoingIntents[typeof(MessageSentBySaga).FullName]);
            Assert.AreEqual("Publish", outgoingIntents[typeof(MessagePublishedBySaga).FullName]);
        }

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.DisableFeature<AutoSubscribe>();
                    c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME);

                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MessagePublishedBySaga), typeof(EndpointThatIsHostingTheSaga));
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
                }
            }

            public class MySagaData : ContainSagaData
            {
            }

            class MessageReplyBySagaHandler : IHandleMessages<MessageReplyBySaga>
            {
                public Task Handle(MessageReplyBySaga message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }

            class MessagePublishedBySagaHandler : IHandleMessages<MessagePublishedBySaga>
            {
                public Task Handle(MessagePublishedBySaga message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }

            class MessageReplyToOriginatorBySagaHandler : IHandleMessages<MessageReplyToOriginatorBySaga>
            {
                public Task Handle(MessageReplyToOriginatorBySaga message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }

            class MessageSentBySagaHandler : IHandleMessages<MessageSentBySaga>
            {
                public MyContext Context { get; set; }

                public Task Handle(MessageSentBySaga message, IMessageHandlerContext context)
                {
                    Context.Done = true;
                    return Task.FromResult(0);
                }
            }
        }

        [Serializable]
        public class MessageInitiatingSaga : ICommand
        {
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
            public bool Done { get; set; }
            public Guid SagaId { get; set; }
        }
    }

}