namespace ServiceBus.Management.AcceptanceTests.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;

    class When_multiple_messages_are_emitted_by_a_saga : AcceptanceTest
    {
        [SetUp]
        public void ConfigSetup()
        {
            // To configure the SagaAudit plugin
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var appSettings = (AppSettingsSection)config.GetSection("appSettings");
            appSettings.Settings.Add("ServiceControl/Queue", ServiceControlInstanceName);
            config.Save();
            ConfigurationManager.RefreshSection("appSettings");
        }

        [TearDown]
        public void ConfigTeardown()
        {
            // Cleanup the saga audit plugin configuration to not leak into other tests
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var appSettings = (AppSettingsSection)config.GetSection("appSettings");
            appSettings.Settings.Remove("ServiceControl/Queue");
            config.Save();
            ConfigurationManager.RefreshSection("appSettings");
        }

        [Test]
        public async Task Saga_history_can_be_fetched_on_master()
        {
            SagaHistory sagaHistory = null;

            var context = await Define<MyContext>()
                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga {Id = "Id"})))
                .Done(async c =>
                {
                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}", instanceName: ServiceControlInstanceName);
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
                outgoingIntents[message.MessageType] = message.Intent;
            }

            Assert.AreEqual("Reply", outgoingIntents[typeof(MessageReplyBySaga).FullName]);
            Assert.AreEqual("Reply", outgoingIntents[typeof(MessageReplyToOriginatorBySaga).FullName]);
            Assert.AreEqual("Send", outgoingIntents[typeof(MessageSentBySaga).FullName]);
            Assert.AreEqual("Publish", outgoingIntents[typeof(MessagePublishedBySaga).FullName]);
        }

        public class SagaEndpoint : EndpointConfigurationBuilder
        {
            public SagaEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                    {
                        c.EnableFeature<AutoSubscribe>();
                        c.AuditSagaStateChanges(ServiceControlInstanceName);
                        c.AuditProcessedMessagesTo(ServiceControlInstanceName);
                    },
                    publishers => { publishers.RegisterPublisherFor<MessagePublishedBySaga>(typeof(SagaEndpoint)); });
            }

            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public MyContext Context { get; set; }

                public async Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    Context.SagaId = Data.Id;

                    await context.Reply(new MessageReplyBySaga());
                    await ReplyToOriginator(context, new MessageReplyToOriginatorBySaga());
                    await context.SendLocal(new MessageSentBySaga());
                    await context.Publish(new MessagePublishedBySaga());
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
            public bool Done { get; set; }
            public Guid SagaId { get; set; }
        }
    }
}