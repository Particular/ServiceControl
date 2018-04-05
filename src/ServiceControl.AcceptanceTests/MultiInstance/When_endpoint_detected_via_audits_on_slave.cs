namespace ServiceBus.Management.AcceptanceTests.MultiInstance
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Endpoints;

    public class When_endpoint_detected_via_audits_on_slave : AcceptanceTest
    {
        private const string Master = "master";
        private static string AuditMaster = $"{Master}.audit";
        private static string ErrorMaster = $"{Master}.error";
        private const string Slave = "slave";
        private static string AuditSlave = $"{Slave}.audit";
        private static string ErrorSlave = $"{Slave}.error";

        private string addressOfRemote;

        [Test]
        public void Should_be_returned_via_master_api()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<EndpointsView> response;

            Define(context, Slave, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                   bus.SendLocal(new MyMessage());
                }))
                .Done(c => TryGetMany("/api/endpoints/", out response, instanceName: Master) && response.Count == 1)
                .Run(TimeSpan.FromSeconds(40));
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>()
                    .AuditTo(Address.Parse(AuditSlave))
                    .ErrorTo(Address.Parse(ErrorMaster));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                }
            }
        }

        void ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues(string instanceName, Settings settings)
        {
            switch (instanceName)
            {
                case Slave:
                    addressOfRemote = settings.ApiUrl;
                    settings.AuditQueue = Address.Parse(AuditSlave);
                    settings.ErrorQueue = Address.Parse(ErrorSlave);
                    break;
                case Master:
                    settings.RemoteInstances = new[]
                    {
                        new RemoteInstanceSetting
                        {
                            ApiUri = addressOfRemote,
                            QueueAddress = Slave
                        }
                    };
                    settings.AuditQueue = Address.Parse(AuditMaster);
                    settings.ErrorQueue = Address.Parse(ErrorMaster);
                    break;
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
        }
    }
}