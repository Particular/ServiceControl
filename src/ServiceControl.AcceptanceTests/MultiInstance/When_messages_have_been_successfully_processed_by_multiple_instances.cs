namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;

    public class When_messages_have_been_successfully_processed_by_multiple_instances : AcceptanceTest
    {
        private const string Master = "master";
        private const string AuditMaster = "audit";
        private const string ErrorMaster = "error";
        private const string Remote1 = "remote1";
        private const string AuditRemote = "audit1";
        private const string ErrorRemote = "error1";

        private string addressOfRemote;


        [Test]
        public void Should_be_found_in_search_by_messageType()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<MessagesView> response;

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
        }

        [Test]
        [Ignore("Times out, investigate")]
        public void Should_be_found_in_search_by_messageId()
        {
            var context = new MyContext();
            List<MessagesView> response;

            Define(context, Remote1, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) => { bus.Send(new MyMessage()); }))
                .WithEndpoint<ReceiverRemote>()
                .Done(c => c.MessageId != null && TryGetMany("/api/messages/search/" + c.MessageId, out response, instanceName: Master))
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
                    settings.RemoteInstances = new[] {addressOfRemote};
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
                    Context.MessageId = Bus.CurrentMessageContext.Id;

                    Thread.Sleep(200);
                }
            }
        }

        public class ReceiverRemote : EndpointConfigurationBuilder
        {
            public ReceiverRemote()
            {
                EndpointSetup<DefaultServerWithAudit>().AuditTo(Address.Parse(AuditRemote));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.MessageId = Bus.CurrentMessageContext.Id;

                    Thread.Sleep(200);
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
            public string PropertyToSearchFor { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string PropertyToSearchFor { get; set; }
        }
    }
}