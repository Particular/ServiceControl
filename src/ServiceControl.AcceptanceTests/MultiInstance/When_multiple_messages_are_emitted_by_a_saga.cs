namespace ServiceBus.Management.AcceptanceTests.MultiInstance
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;

    public class When_multiple_messages_are_emitted_by_a_saga : AcceptanceTest
    {
        [SetUp]
        public void SetUp()
        {
            // To configure the SagaAudit plugin
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var appSettings = (AppSettingsSection)config.GetSection("appSettings");
            appSettings.Settings.Add("ServiceControl/Queue", Remote1);
            config.Save();
            ConfigurationManager.RefreshSection("appSettings");
        }

        [TearDown]
        public void TearDown()
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
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            SagaHistory sagaHistory = null;

            var context = await Define<MyContext>(Remote1, Master)
                .WithEndpoint<EndpointThatIsHostingTheSaga>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga {Id = "Id"})))
                .Done(async c =>
                {
                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}", instanceName: Master);
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

            Assert.AreEqual("Reply", outgoingIntents[typeof(MessageReplyBySaga).FullName]);
            Assert.AreEqual("Reply", outgoingIntents[typeof(MessageReplyToOriginatorBySaga).FullName]);
            Assert.AreEqual("Send", outgoingIntents[typeof(MessageSentBySaga).FullName]);
            Assert.AreEqual("Publish", outgoingIntents[typeof(MessagePublishedBySaga).FullName]);
        }

        private void ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues(string instanceName, Settings settings)
        {
            switch (instanceName)
            {
                case Remote1:
                    addressOfRemote = settings.ApiUrl;
                    settings.AuditQueue = AuditRemote;
                    settings.ErrorQueue = ErrorRemote;
                    break;
                case Master:
                    settings.RemoteInstances = new[]
                    {
                        new RemoteInstanceSetting
                        {
                            ApiUri = addressOfRemote,
                            QueueAddress = Remote1
                        }
                    };
                    settings.AuditQueue = AuditMaster;
                    settings.ErrorQueue = ErrorMaster;
                    break;
            }
        }

        private string addressOfRemote;
        private const string Master = "master";
        private const string Remote1 = "remote1";
        private static string AuditMaster = $"{Master}.audit";
        private static string ErrorMaster = $"{Master}.error";
        private static string AuditRemote = $"{Remote1}.audit1";
        private static string ErrorRemote = $"{Remote1}.error1";

        public class EndpointThatIsHostingTheSaga : EndpointConfigurationBuilder
        {
            public EndpointThatIsHostingTheSaga()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                    {
                        c.EnableFeature<AutoSubscribe>();
                        c.AuditSagaStateChanges(Remote1);
                        c.AuditProcessedMessagesTo(AuditRemote);
                        c.SendFailedMessagesTo(ErrorMaster);
                    },
                    publishers => { publishers.RegisterPublisherFor<MessagePublishedBySaga>(typeof(EndpointThatIsHostingTheSaga)); });
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