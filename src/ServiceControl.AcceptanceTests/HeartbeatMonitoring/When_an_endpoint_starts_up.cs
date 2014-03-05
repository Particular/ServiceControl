namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System;
    using System.Linq;
    using Contexts;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.EventLog;

    public class When_an_endpoint_starts_up : AcceptanceTest
    {
        [Test]
        public void Should_result_in_a_startup_event()
        {
            var context = new MyContext();

            EventLogItem entry = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<StartingEndpoint>()
                .Done(c => TryGetSingle("/api/eventlogitems/", out entry, e => e.RelatedTo.Any(r => r.Contains(typeof(StartingEndpoint).Name)) && e.EventType == typeof(EndpointStarted).Name))
                .Run();

            Assert.AreEqual(Severity.Info, entry.Severity, "Endpoint startup should be treated as info");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/host/" + context.HostId));
           
        }


        public class MyContext : ScenarioContext
        {
            public Guid HostId { get; set; }
        }

        public class StartingEndpoint : EndpointConfigurationBuilder
        {
            
            public StartingEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            class HostIdFinder:IWantToRunWhenConfigurationIsComplete
            {
                public UnicastBus UnicastBus { get; set; }

                public MyContext Context { get; set; }

                public void Run()
                {
                    Context.HostId = UnicastBus.HostInformation.HostId;
                }
            }
        }
    }
}