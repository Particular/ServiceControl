namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Settings;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    public class When_messages_have_been_successfully_processed_by_multiple_instances : AcceptanceTest
    {
        private const string Master = "master";
        private static string AuditMaster = $"{Master}.audit";
        private static string ErrorMaster = $"{Master}.error";
        private const string Remote1 = "remote1";
        private static string AuditRemote = $"{Remote1}.audit";
        private static string ErrorRemote = $"{Remote1}.error";

        private string addressOfRemote;

        [Test]
        public void Should_be_reported_by_get_messages_api()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<MessagesView> response = new List<MessagesView>();

            Define(context, Remote1, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                    bus.SendLocal(new MyMessage());
                }))
                .WithEndpoint<ReceiverRemote>()
                .Done(c => TryGetMany("/api/messages/", out response, instanceName: Master) && response.Count == 2)
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

        [Test]
        public void Should_be_found_in_search_by_messageType()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<MessagesView> response = new List<MessagesView>();

            //search for the message type
            var searchString = typeof(MyMessage).Name;

            Define(context, Remote1, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                    bus.SendLocal(new MyMessage());
                }))
                .WithEndpoint<ReceiverRemote>()
                .Done(c => TryGetMany("/api/messages/search/" + searchString, out response, instanceName: Master) && response.Count == 2)
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

        [Test]
        public void Should_be_found_in_search_by_messageId()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<MessagesView> response;

            Define(context, Remote1, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) => { bus.Send(new MyMessage()); }))
                .WithEndpoint<ReceiverRemote>()
                .Done(c => c.Remote1MessageId != null && TryGetMany("/api/messages/search/" + c.Remote1MessageId, out response, instanceName: Master))
                .Run(TimeSpan.FromSeconds(40));
        }

        [Test]
        public void Should_be_found_in_query_by_conversationId()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<MessagesView> response;

            Define(context, Remote1, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) => { bus.SendLocal(new TriggeringMessage()); }))
                .WithEndpoint<ReceiverRemote>()
                .Done(c => c.ConversationId != null && TryGetMany($"/api/conversations/{c.ConversationId}", out response, instanceName: Master) && response.Count == 2)
                .Run(TimeSpan.FromSeconds(40));
        }

        [Test]
        public void Should_be_reported_by_endpoint_get_messages_api()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<MessagesView> response = new List<MessagesView>();

            var endpointName = Conventions.EndpointNamingConvention(typeof(ReceiverRemote));

            Define(context, Remote1, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<ReceiverRemote>()
                .Done(c => TryGetMany($"/api/endpoints/{endpointName}/messages/", out response, instanceName: Master) && response.Count == 1)
                .Run(TimeSpan.FromSeconds(40));

            var expectedRemote1InstanceId = InstanceIdGenerator.FromApiUrl(SettingsPerInstance[Remote1].ApiUrl);

            var remote1Message = response.SingleOrDefault(msg => msg.MessageId == context.Remote1MessageId);

            Assert.NotNull(remote1Message, "Remote1 message not found");
            Assert.AreEqual(expectedRemote1InstanceId, remote1Message.InstanceId, "Remote1 instance id mismatch");
        }

        [Test]
        public void Should_be_found_in_endpoint_search_by_messageType()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<MessagesView> response = new List<MessagesView>();

            var endpointName = Conventions.EndpointNamingConvention(typeof(ReceiverRemote));

            //search for the message type
            var searchString = typeof(MyMessage).Name;

            Define(context, Remote1, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<ReceiverRemote>()
                .Done(c => TryGetMany($"/api/endpoints/{endpointName}/messages/search/" + searchString, out response, instanceName: Master) && response.Count == 1)
                .Run(TimeSpan.FromSeconds(40));

            var expectedRemote1InstanceId = InstanceIdGenerator.FromApiUrl(SettingsPerInstance[Remote1].ApiUrl);

            var remote1Message = response.SingleOrDefault(msg => msg.MessageId == context.Remote1MessageId);

            Assert.NotNull(remote1Message, "Remote1 message not found");
            Assert.AreEqual(expectedRemote1InstanceId, remote1Message.InstanceId, "Remote1 instance id mismatch");
        }

        [Test]
        public void Should_list_the_endpoints_in_the_list_of_known_endpoints()
        {
            var context = new MyContext();
            List<KnownEndpointsView> knownEndpoints = null;

            Define(context, Remote1, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<ReceiverRemote>()
                .Done(c => TryGetMany("/api/endpoints/known", out knownEndpoints, m => m.EndpointDetails.Name == context.EndpointNameOfReceivingEndpoint, Master))
                .Run(TimeSpan.FromSeconds(20));

            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, knownEndpoints.Single(e => e.EndpointDetails.Name == context.EndpointNameOfReceivingEndpoint).EndpointDetails.Name);
            Assert.AreEqual(Environment.MachineName, knownEndpoints.Single(e => e.EndpointDetails.Name == context.EndpointNameOfReceivingEndpoint).HostDisplayName);
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
                    .AddMapping<MyMessage>(typeof(ReceiverRemote))
                    .AddMapping<TriggeredMessage>(typeof(ReceiverRemote));
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

                    Thread.Sleep(200);
                }
            }

            public class TriggeringMessageHandler : IHandleMessages<TriggeringMessage>
            {
                public MyContext Context { get; set; }
                public IBus Bus { get; set; }
                public void Handle(TriggeringMessage message)
                {
                    Context.ConversationId = Bus.CurrentMessageContext.Headers[Headers.ConversationId];
                    Bus.Send(new TriggeredMessage());
                    Thread.Sleep(200);
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

                    Thread.Sleep(200);
                }
            }

            public class TriggeredMessageHandler : IHandleMessages<TriggeredMessage>
            {
                public MyContext Context { get; set; }
                public IBus Bus { get; set; }

                public void Handle(TriggeredMessage message)
                {
                    Context.ConversationId = Bus.CurrentMessageContext.Headers[Headers.ConversationId];

                    Thread.Sleep(200);
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        public class TriggeringMessage : ICommand
        {

        }

        public class TriggeredMessage : ICommand
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