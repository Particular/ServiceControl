namespace ServiceBus.Management.AcceptanceTests.MultiInstance
{
    using System;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    public class When_remote_instance_is_not_reachable : AcceptanceTest
    {
        [Test]
        public async Task Should_not_fail()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            //search for the message type
            var searchString = typeof(MyMessage).Name;

            await Define<MyContext>(Master)
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .Done(async c => await this.TryGetMany<MessagesView>("/messages/search/" + searchString, instanceName: Master))
                .Run(TimeSpan.FromSeconds(40));
        }

        private void ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues(string instanceName, Settings settings)
        {
            addressOfRemote = "http://localhost:12121";
            settings.RemoteInstances = new[]
            {
                new RemoteInstanceSetting
                {
                    ApiUri = addressOfRemote,
                    QueueAddress = "remote1"
                }
            };
            settings.AuditQueue = AuditMaster;
            settings.ErrorQueue = ErrorMaster;
        }

        private string addressOfRemote;
        private const string Master = "master";
        private static string AuditMaster = $"{Master}.audit";
        private static string ErrorMaster = $"{Master}.error";

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.AuditProcessedMessagesTo(AuditMaster);
                    c.SendFailedMessagesTo(ErrorMaster);
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.MessageId = context.MessageId;
                    return Task.FromResult(0);
                }
            }
        }


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}