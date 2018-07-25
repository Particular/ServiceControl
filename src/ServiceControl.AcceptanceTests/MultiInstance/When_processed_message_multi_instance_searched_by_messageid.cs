namespace ServiceBus.Management.AcceptanceTests.MultiInstance
{
    using System;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    public class When_processed_message_multi_instance_searched_by_messageid : AcceptanceTest
    {
        [Test]
        public async Task Should_be_found()
        {
            SetSetInstanceSettings(ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues);

            await Define<MyContext>(Remote1, Master)
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage())))
                .WithEndpoint<ReceiverRemote>()
                .Done(async c => c.Remote1MessageId != null && await this.TryGetMany<MessagesView>("/api/messages/search/" + c.Remote1MessageId, instanceName: Master))
                .Run(TimeSpan.FromSeconds(40));
        }

        void ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues(string instanceName, Settings settings)
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

        string addressOfRemote;
        const string Master = "master";
        const string Remote1 = "remote1";
        const string ReceiverHostDisplayName = "Rico";
        static string AuditMaster = $"{Master}.audit";
        static string ErrorMaster = $"{Master}.error";
        static string AuditRemote = $"{Remote1}.audit";
        static string ErrorRemote = $"{Remote1}.error";

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


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string Remote1MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}