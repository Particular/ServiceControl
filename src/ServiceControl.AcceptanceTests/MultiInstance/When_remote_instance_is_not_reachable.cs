﻿namespace ServiceBus.Management.AcceptanceTests
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

    public class When_remote_instance_is_not_reachable : AcceptanceTest
    {
        private const string Master = "master";
        private const string AuditMaster = "audit";
        private const string ErrorMaster = "error";

        private string addressOfRemote;


        [Test]
        public void Should_not_fail()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<MessagesView> response;

            //search for the message type
            var searchString = typeof(MyMessage).Name;

            Define(context, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) => { bus.SendLocal(new MyMessage()); }))
                .Done(c => TryGetMany("/api/messages/search/" + searchString, out response, instanceName: Master))
                .Run(TimeSpan.FromSeconds(40));
        }

        private void ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues(string instanceName, Settings settings)
        {
            addressOfRemote = "http://localhost:12121";
            settings.RemoteInstances = new List<Settings.RemoteInstanceSetting>
            {
                new Settings.RemoteInstanceSetting
                {
                    Uri = addressOfRemote,
                    Address = "remote1"
                }
            };
            settings.AuditQueue = Address.Parse(AuditMaster);
            settings.ErrorQueue = Address.Parse(ErrorMaster);
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>();
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