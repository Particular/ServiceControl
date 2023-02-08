namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EventLog;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestSupport.EndpointTemplates;

    [TestFixture]
    class When_custom_check_events_are_triggered : AcceptanceTest
    {
        [Test]
        public async Task Should_result_in_a_custom_check_failed_event()
        {
            EventLogItem entry = null;

            await Define<MyContext>()
                .WithEndpoint<EndpointWithCustomCheck>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EventLogItem>("/api/eventlogitems/", e => e.EventType == nameof(Contracts.CustomChecks.CustomCheckFailed));
                    entry = result;
                    return result;
                })
                .Run();

            Assert.AreEqual(Severity.Error, entry.Severity, "Failed custom checks should be treated as error");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/customcheck/EventuallyFailingCustomCheck"));
        }

        public class MyContext : ScenarioContext
        {
        }

        public class EndpointWithCustomCheck : EndpointConfigurationBuilder
        {
            public EndpointWithCustomCheck()
            {
                EndpointSetup<DefaultServer>(c => { c.ReportCustomChecksTo(Settings.DEFAULT_SERVICE_NAME, TimeSpan.FromSeconds(1)); });
            }

            public class EventuallyFailingCustomCheck : CustomCheck
            {
                public EventuallyFailingCustomCheck()
                    : base("EventuallyFailingCustomCheck", "Testing", TimeSpan.FromSeconds(1))
                {
                }

                public override Task<CheckResult> PerformCheck()
                {
#pragma warning disable IDE0047 // Remove unnecessary parentheses
                    if ((Interlocked.Increment(ref counter) / 10) % 2 == 0)
#pragma warning restore IDE0047 // Remove unnecessary parentheses
                    {
                        return Task.FromResult(CheckResult.Failed("fail!"));
                    }

                    return Task.FromResult(CheckResult.Pass);
                }

                static int counter;
            }
        }
    }
}