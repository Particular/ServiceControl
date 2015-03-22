﻿namespace Particular.Backend.Debugging.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Saga;
    using NUnit.Framework;
    using Particular.Backend.Debugging.AcceptanceTests.Contexts;
    using Particular.Backend.Debugging.Api;

    public class When_multiple_messages_are_emitted_by_a_saga : AcceptanceTest
    {
        [Test]
        public void All_outgoing_message_intents_should_be_captured()
        {
            var context = new MyContext();
            SagaHistory sagaHistory = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.Given((bus, c) => bus.SendLocal(new MessageInitiatingSaga())))
                .Done(c => c.Done && TryGet("/api/sagas/" + c.SagaId, out sagaHistory))
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
                EndpointSetup<DefaultServer>(c => Configure.Features.Disable<AutoSubscribe>())
                    .AuditTo(Address.Parse("audit"))
                    .AddMapping<MessagePublishedBySaga>(typeof(EndpointThatIsHostingTheSaga));
            }

            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public MyContext Context { get; set; }

                public void Handle(MessageInitiatingSaga message)
                {
                    Context.SagaId = Data.Id;

                    Bus.Reply(new MessageReplyBySaga());
                    ReplyToOriginator(new MessageReplyToOriginatorBySaga());
                    Bus.SendLocal(new MessageSentBySaga());
                    Bus.Publish(new MessagePublishedBySaga());
                }
            }

            public class MySagaData : ContainSagaData
            {
            }

            class MessageReplyBySagaHandler : IHandleMessages<MessageReplyBySaga>
            {
                public MyContext Context { get; set; }

                public void Handle(MessageReplyBySaga message)
                {
                    Context.Done = true;
                }
            }

            class MessagePublishedBySagaHandler : IHandleMessages<MessagePublishedBySaga>
            {
                public void Handle(MessagePublishedBySaga message)
                {
                }
            }

            class MessageReplyToOriginatorBySagaHandler : IHandleMessages<MessageReplyToOriginatorBySaga>
            {
                public void Handle(MessageReplyToOriginatorBySaga message)
                {
                }
            }

            class MessageSentBySagaHandler : IHandleMessages<MessageSentBySaga>
            {
                public MyContext Context { get; set; }

                public void Handle(MessageSentBySaga message)
                {
                    //Context.Done = true;
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