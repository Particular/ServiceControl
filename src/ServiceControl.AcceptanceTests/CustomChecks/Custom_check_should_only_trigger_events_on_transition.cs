namespace ServiceBus.Management.AcceptanceTests.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading;
    using Contexts;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.EventLog;
    using ServiceControl.Plugin.CustomChecks;

    [TestFixture]
    public class Custom_check_should_only_trigger_events_on_transition : AcceptanceTest
    {
        [Test]
        public void Should_result_in_a_custom_check_failed_event()
        {
            var context = new MyContext();

            EventLogItem entry = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<EndpointWithFailingCustomCheck>()
                .Done(c => TryGetSingle("/api/eventlogitems/", out entry, e => e.EventType == typeof(CustomCheckFailed).Name))
                .Run();

            Assert.AreEqual(Severity.Error, entry.Severity, "Failed custom checks should be treated as error");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/customcheck/EventuallyFailingCustomCheck"));
        }

        public class MyContext : ScenarioContext
        {
            public string CustomCheckId { get; set; }
        }

        public class EndpointWithFailingCustomCheck : EndpointConfigurationBuilder
        {

            public EndpointWithFailingCustomCheck()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            public class EventuallyFailingCustomCheck : PeriodicCheck
            {
                private static int counter;

                public EventuallyFailingCustomCheck()
                    : base("EventuallyFailingCustomCheck", "Testing", TimeSpan.FromSeconds(1)) { }

                public override CheckResult PerformCheck()
                {
                    if ((Interlocked.Increment(ref counter) / 10) % 2 == 0)
                    {
                        return CheckResult.Failed("fail!");
                    }
                    return CheckResult.Pass;
                }
            }
        }
    }
}
