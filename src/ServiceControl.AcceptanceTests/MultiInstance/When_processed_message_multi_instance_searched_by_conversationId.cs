namespace ServiceBus.Management.AcceptanceTests.MultiInstance
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;

    public class When_processed_message_multi_instance_searched_by_conversationId : AcceptanceTest
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

            await Define<MyContext>(Remote1, Master)
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.SendLocal(new TriggeringMessage())))
                .WithEndpoint<ReceiverRemote>()
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MessagesView>($"/api/conversations/{c.ConversationId}", instanceName: Master);
                    List<MessagesView> response = result;
                    return c.ConversationId != null && result && response.Count == 2;
                })
                .Run(TimeSpan.FromSeconds(40));
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
                        .RouteToEndpoint(typeof(TriggeredMessage), typeof(ReceiverRemote));
                });
            }

            public class TriggeringMessageHandler : IHandleMessages<TriggeringMessage>
            {
                public MyContext Context { get; set; }
                public Task Handle(TriggeringMessage message, IMessageHandlerContext context)
                {
                    Context.ConversationId = context.MessageHeaders[Headers.ConversationId];
                    return context.Send(new TriggeredMessage());
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

                    // TODO: Do this
                    // c.UniquelyIdentifyRunningInstance().UsingNames()
                });
            }

            public class TriggeredMessageHandler : IHandleMessages<TriggeredMessage>
            {
                public MyContext Context { get; set; }

                public Task Handle(TriggeredMessage message, IMessageHandlerContext context)
                {
                    Context.ConversationId = context.MessageHeaders[Headers.ConversationId];
                    return Task.FromResult(0);
                }
            }
        }

        public class TriggeringMessage : ICommand
        {

        }

        public class TriggeredMessage : ICommand
        {

        }

        public class MyContext : ScenarioContext
        {
            public string ConversationId { get; set; }
        }
    }
}