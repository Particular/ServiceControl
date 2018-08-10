﻿namespace ServiceBus.Management.AcceptanceTests.MultiInstance
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

    public class When_processed_message_multi_instance_by_messages : AcceptanceTest
    {
        [Test]
        public async Task Should_be_found()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var response = new List<MessagesView>();

            var context = await Define<MyContext>(Remote1, Master)
                .WithEndpoint<Sender>(b => b.When(async (bus, c) =>
                {
                    await bus.Send(new MyMessage());
                    await bus.SendLocal(new MyMessage());
                }))
                .WithEndpoint<ReceiverRemote>()
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MessagesView>("/messages/", instanceName: Master);
                    response = result;
                    return result && response.Count == 2;
                })
                .Run(TimeSpan.FromSeconds(40));

            Assert.AreNotEqual(Instances[Master].Id, Instances[Remote1].Id);

            var masterMessage = response.SingleOrDefault(msg => msg.MessageId == context.MasterMessageId);

            Assert.NotNull(masterMessage, "Master message not found");
            Assert.AreEqual(Instances[Master].Id, masterMessage.InstanceId, "Master instance id mismatch");

            var remote1Message = response.SingleOrDefault(msg => msg.MessageId == context.Remote1MessageId);

            Assert.NotNull(remote1Message, "Remote1 message not found");
            Assert.AreEqual(Instances[Remote1].Id, remote1Message.InstanceId, "Remote1 instance id mismatch");
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
            public string MasterMessageId { get; set; }
            public string Remote1MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}