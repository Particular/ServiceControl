﻿namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Settings;
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

            List<KnownEndpointsView> knownEndpoints = null;
            HttpResponseMessage httpResponseMessage = null;

            var context = await Define<MyContext>(Remote1, Master)
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage())))
                .WithEndpoint<ReceiverRemote>()
                .Done(async c =>
                {
                    var result = await this.TryGetMany<KnownEndpointsView>("/api/endpoints/known", m => m.EndpointDetails.Name == c.EndpointNameOfReceivingEndpoint, Master);
                    knownEndpoints = result;
                    if (result)
                    {
                        httpResponseMessage = await this.GetRaw("/api/endpoints/known", Master);

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

                    // TODO: Figure out how to do this properly
                    // c.UniquelyIdentifyRunningInstance().UsingNames();
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

            public string ConversationId { get; set; }
        }
    }
}