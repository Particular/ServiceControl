namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
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
    using ServiceControl.CompositeViews.Endpoints;

    public class When_processed_message_multi_instance_endpoint_processing : AcceptanceTest
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
        public async Task Should_be_listed_in_known_endpoints()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<KnownEndpointsView> knownEndpoints = null;
            HttpResponseMessage httpResponseMessage = null;

            await Define(context, Remote1, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<ReceiverRemote>()
                .Done(async c =>
                {
                    var result = await TryGetMany<KnownEndpointsView>("/api/endpoints/known", m => m.EndpointDetails.Name == context.EndpointNameOfReceivingEndpoint, Master);
                    knownEndpoints = result;
                    if (result)
                    {
                        httpResponseMessage = await GetRaw("/api/endpoints/known", Master);

                        return true;
                    }
                    return false;
                })
                .Run(TimeSpan.FromSeconds(20));

            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, knownEndpoints.Single(e => e.EndpointDetails.Name == context.EndpointNameOfReceivingEndpoint).EndpointDetails.Name);
            Assert.AreEqual(ReceiverHostDisplayName, knownEndpoints.Single(e => e.EndpointDetails.Name == context.EndpointNameOfReceivingEndpoint).HostDisplayName);

            Assert.NotNull(httpResponseMessage);
            Assert.False(httpResponseMessage.Headers.Contains("ETag"), "Expected not to contain ETag header, but it was found.");
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

            public string ConversationId { get; set; }
        }
    }
}