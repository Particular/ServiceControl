namespace ServiceBus.Management.AcceptanceTests.HeartbeatMonitoring
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Contracts.EndpointControl;
    using ServiceControl.EventLog;

    public class When_an_endpoint_starts_up : AcceptanceTest
    {
        [Test]
        public async Task Should_result_in_a_startup_event()
        {
            var context = new MyContext();
            EventLogItem entry = null;

            await Define(context)
                .WithEndpoint<StartingEndpoint>()
                .Done(async c =>
                {
                    var result = await TryGetSingle<EventLogItem>("/api/eventlogitems/", e => e.RelatedTo.Any(r => r.Contains(typeof(StartingEndpoint).Name)) && e.EventType == typeof(EndpointStarted).Name);
                    entry = result;
                    return result;
                })
                .Run();

            Assert.AreEqual(Severity.Info, entry.Severity, "Endpoint startup should be treated as info");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/host/" + context.HostIdentifier));
        }

        public class MyContext : ScenarioContext
        {
            public Guid HostIdentifier { get; set; }
        }

        public class StartingEndpoint : EndpointConfigurationBuilder
        {
            public StartingEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var hostIdentifier = Guid.NewGuid();
                    c.GetSettings().Set("ServiceControl.CustomHostIdentifier", hostIdentifier);
                    c.UniquelyIdentifyRunningInstance().UsingCustomIdentifier(hostIdentifier);
                }).IncludeAssembly(Assembly.LoadFrom("ServiceControl.Plugin.Nsb5.Heartbeat.dll"));
            }

            class RetrieveHostIdentifier : IWantToRunWhenBusStartsAndStops
            {
                readonly ReadOnlySettings settings;
                readonly MyContext context;

                public RetrieveHostIdentifier(ReadOnlySettings settings, MyContext context)
                {
                    this.settings = settings;
                    this.context = context;
                }

                public void Start()
                {
                    context.HostIdentifier = settings.Get<Guid>("ServiceControl.CustomHostIdentifier");
                }

                public void Stop()
                {
                }
            }
        }
    }
}