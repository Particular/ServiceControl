namespace ServiceBus.Management.AcceptanceTests.MultiInstance
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.Monitoring;

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
        public async Task Should_be_returned_via_master_api()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<EndpointsView> response;

            await Define(context, Slave, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                   bus.SendLocal(new MyMessage());
                }))
                .Done(async c =>
                {
                    var result =  await TryGetMany<EndpointsView>("/api/endpoints/", instanceName: Master);
                    response = result;
                    return result && response.Count == 1;
                })
                .Run(TimeSpan.FromSeconds(40));
        }

        [Test]
        public async Task Should_be_configurable_on_master()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();
            List<EndpointsView> response = null;

            await Define(context, Slave, Master)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.SendLocal(new MyMessage());
                }))
                .Done(async c =>
                {
                    var result = await TryGetMany<EndpointsView>("/api/endpoints/", instanceName: Master);
                    response = result;
                    if (result && response.Count > 0)
                    {
                        c.EndpointKnownOnMaster = true;
                    }

                    if (c.EndpointKnownOnMaster)
                    {
                        var endpointId = response.First().Id;

                        await Patch($"/api/endpoints/{endpointId}", new EndpointUpdateModel
                        {
                            MonitorHeartbeat = true
                        }, Master);

                        var resultAfterPath = await TryGetMany<EndpointsView>("/api/endpoints/", instanceName: Master);
                        response = resultAfterPath;
                        return resultAfterPath;
                    }

                    return false;
                })
                .Run(TimeSpan.FromSeconds(40));

            Assert.IsNotNull(response.First());
            Assert.IsTrue(response.First().MonitorHeartbeat);
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
            public bool EndpointKnownOnMaster { get; set; }
        }
    }
}