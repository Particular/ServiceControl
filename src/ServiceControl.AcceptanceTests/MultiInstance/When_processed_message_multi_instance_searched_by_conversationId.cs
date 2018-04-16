namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Hosting;
    using NServiceBus.Settings;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
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

            var context = new MyContext();

            await Define(context, Remote1, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) => { bus.SendLocal(new TriggeringMessage()); }))
                .WithEndpoint<ReceiverRemote>()
                .Done(async c =>
                {
                    var result = await TryGetMany<MessagesView>($"/api/conversations/{c.ConversationId}", instanceName: Master);
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
                    settings.AuditQueue = Address.Parse(AuditRemote);
                    settings.ErrorQueue = Address.Parse(ErrorRemote);
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
                    settings.AuditQueue = Address.Parse(AuditMaster);
                    settings.ErrorQueue = Address.Parse(ErrorMaster);
                    break;
            }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>()
                    .AuditTo(Address.Parse(AuditMaster))
                    .ErrorTo(Address.Parse(ErrorMaster))
                    .AddMapping<TriggeredMessage>(typeof(ReceiverRemote));
            }

            public class TriggeringMessageHandler : IHandleMessages<TriggeringMessage>
            {
                public MyContext Context { get; set; }
                public IBus Bus { get; set; }
                public void Handle(TriggeringMessage message)
                {
                    Context.ConversationId = Bus.CurrentMessageContext.Headers[Headers.ConversationId];
                    Bus.Send(new TriggeredMessage());
                }
            }
        }

        public class ReceiverRemote : EndpointConfigurationBuilder
        {
            public ReceiverRemote()
            {
                EndpointSetup<DefaultServerWithAudit>()
                    .AuditTo(Address.Parse(AuditRemote))
                    .ErrorTo(Address.Parse(ErrorRemote));
            }

            public class TriggeredMessageHandler : IHandleMessages<TriggeredMessage>
            {
                public MyContext Context { get; set; }
                public IBus Bus { get; set; }

                public void Handle(TriggeredMessage message)
                {
                    Context.ConversationId = Bus.CurrentMessageContext.Headers[Headers.ConversationId];
                }
            }
        }

        // Needed to override the host display name for ReceiverRemote endpoint
        // (.UniquelyIdentifyRunningInstance().UsingNames(instanceName, hostName) didn't work)
        public class HostIdFixer : IWantToRunWhenConfigurationIsComplete
        {

            public HostIdFixer(UnicastBus bus, ReadOnlySettings settings)
            {
                var hostId = CreateGuid(Environment.MachineName, settings.EndpointName());
                var location = Assembly.GetExecutingAssembly().Location;
                var properties = new Dictionary<string, string>
                {
                    {"Location", location}
                };
                bus.HostInformation = new HostInformation(
                    hostId: hostId,
                    displayName: ReceiverHostDisplayName,
                    properties: properties);
            }

            static Guid CreateGuid(params string[] data)
            {
                using (var provider = new MD5CryptoServiceProvider())
                {
                    var inputBytes = Encoding.Default.GetBytes(string.Concat(data));
                    var hashBytes = provider.ComputeHash(inputBytes);
                    return new Guid(hashBytes);
                }
            }

            public void Run(Configure config)
            {
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