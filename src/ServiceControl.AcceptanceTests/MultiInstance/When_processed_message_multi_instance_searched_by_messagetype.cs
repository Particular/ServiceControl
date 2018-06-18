namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Settings;

    public class When_processed_message_multi_instance_searched_by_messagetype : AcceptanceTest
    {
        private const string Master = "master";
        private static string AuditMaster = $"{Master}.audit";
        private static string ErrorMaster = $"{Master}.error";
        private const string Remote1 = "remote1";
        private static string AuditRemote = $"{Remote1}.audit";
        private static string ErrorRemote = $"{Remote1}.error";
        private const string ReceiverHostDisplayName = "Rico";

        private string addressOfRemote;

        [Test]
        public async Task Should_be_found()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<MessagesView> response = new List<MessagesView>();

            //search for the message type
            var searchString = typeof(MyMessage).Name;

            await Define(context, Remote1, Master)
                .WithEndpoint<Sender>(b => b.When(async (bus, c) =>
                {
                    await bus.Send(new MyMessage());
                    await bus.SendLocal(new MyMessage());
                }))
                .WithEndpoint<ReceiverRemote>()
                .Done(async c =>
                {
                    var result = await TryGetMany<MessagesView>("/api/messages/search/" + searchString, instanceName: Master);
                    response = result;
                    return result && response.Count == 2;
                })
                .Run(TimeSpan.FromSeconds(40));

            var expectedMasterInstanceId = InstanceIdGenerator.FromApiUrl(SettingsPerInstance[Master].ApiUrl);
            var expectedRemote1InstanceId = InstanceIdGenerator.FromApiUrl(SettingsPerInstance[Remote1].ApiUrl);

            var masterMessage = response.SingleOrDefault(msg => msg.MessageId == context.MasterMessageId);

            Assert.NotNull(masterMessage, "Master message not found");
            Assert.AreEqual(expectedMasterInstanceId, masterMessage.InstanceId, "Master instance id mismatch");

            var remote1Message = response.SingleOrDefault(msg => msg.MessageId == context.Remote1MessageId);

            Assert.NotNull(remote1Message, "Remote1 message not found");
            Assert.AreEqual(expectedRemote1InstanceId, remote1Message.InstanceId, "Remote1 instance id mismatch");
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

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                    {
                        c.AuditProcessedMessagesTo(AuditMaster);
                        c.SendFailedMessagesTo(ErrorMaster);

                        c.ConfigureTransport()
                            .Routing()
                            .RouteToEndpoint(typeof(MyMessage), typeof(ReceiverRemote));
                    });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.MasterMessageId = context.MessageId;
                    return Task.FromResult(0);
                }
            }
        }

        public class ReceiverRemote : EndpointConfigurationBuilder
        {
            public ReceiverRemote()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.AuditProcessedMessagesTo(AuditRemote);
                    c.SendFailedMessagesTo(ErrorRemote);

                    // TODO: This
                    // c.UniquelyIdentifyRunningInstance().UsingNames()
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.Remote1MessageId = context.MessageId;
                    return Task.FromResult(0);
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MasterMessageId { get; set; }
            public string Remote1MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}