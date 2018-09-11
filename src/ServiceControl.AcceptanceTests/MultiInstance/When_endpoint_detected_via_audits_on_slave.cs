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
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.Monitoring;

    class When_endpoint_detected_via_audits_on_slave : AcceptanceTest
    {
        [Test]
        public async Task Should_be_configurable_on_master()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;
            CustomInstanceConfiguration = ConfigureWaitingForMasterToSubscribe;

            List<EndpointsView> response = null;

            await Define<MyContext>(Slave, Master)
                .WithEndpoint<Sender>(b => b.When(c => c.HasNativePubSubSupport || c.MasterSubscribed,
                    (bus, c) => bus.SendLocal(new MyMessage())))
                .Done(async c =>
                {
                    var result = await this.TryGetMany<EndpointsView>("/api/endpoints/", instanceName: Master);
                    response = result;
                    if (result && response.Count > 0)
                    {
                        c.EndpointKnownOnMaster = true;
                    }

                    if (c.EndpointKnownOnMaster)
                    {
                        var endpointId = response.First().Id;

                        await this.Patch($"/api/endpoints/{endpointId}", new EndpointUpdateModel
                        {
                            MonitorHeartbeat = true
                        }, Master);

                        var resultAfterPath = await this.TryGetMany<EndpointsView>("/api/endpoints/", instanceName: Master);
                        response = resultAfterPath;
                        return resultAfterPath;
                    }

                    return false;
                })
                .Run(TimeSpan.FromSeconds(40));

            Assert.IsNotNull(response.First());
            Assert.IsTrue(response.First().MonitorHeartbeat);
        }

        void ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues(string instanceName, Settings settings)
        {
            switch (instanceName)
            {
                case Slave:
                    addressOfRemote = settings.ApiUrl;
                    settings.AuditQueue = AuditSlave;
                    settings.ErrorQueue = ErrorSlave;
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
                    settings.AuditQueue = AuditMaster;
                    settings.ErrorQueue = ErrorMaster;
                    break;
            }
        }

        void ConfigureWaitingForMasterToSubscribe(string instance, EndpointConfiguration config)
        {
            if (instance == Slave)
            {
                config.OnEndpointSubscribed<MyContext>((s, ctx) =>
                {
                    if (s.SubscriberReturnAddress.IndexOf(Master, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ctx.MasterSubscribed = true;
                    }
                });
            }
        }

        private string addressOfRemote;
        private const string Master = "master";
        private const string Slave = "slave";
        private static string AuditMaster = $"{Master}.audit";
        private static string ErrorMaster = $"{Master}.error";
        private static string AuditSlave = $"{Slave}.audit";
        private static string ErrorSlave = $"{Slave}.error";

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.AuditProcessedMessagesTo(AuditSlave);
                    c.SendFailedMessagesTo(ErrorMaster);
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public bool EndpointKnownOnMaster { get; set; }
            public bool MasterSubscribed { get; set; }
        }
    }
}