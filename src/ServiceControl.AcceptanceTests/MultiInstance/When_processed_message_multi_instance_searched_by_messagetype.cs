﻿namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                    bus.SendLocal(new MyMessage());
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
                    .AddMapping<MyMessage>(typeof(ReceiverRemote));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.MasterMessageId = Bus.CurrentMessageContext.Id;
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

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.Remote1MessageId = Bus.CurrentMessageContext.Id;
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