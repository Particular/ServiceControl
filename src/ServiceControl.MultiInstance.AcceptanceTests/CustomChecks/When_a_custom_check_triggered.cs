namespace ServiceBus.Management.AcceptanceTests.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Contracts;
    using ServiceControl.EventLog;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    [TestFixture]
    class When_a_custom_check_triggered : AcceptanceTest
    {
        [Test]
        public async Task Should_result_in_report_events()
        {
            EventLogItem failedCheckLogItem = null, passingCheckLogItem = null;

            await Define<MyContext>()
                .WithEndpoint<EndpointWithCustomChecks>()
                .Done(async c =>
                {
                    SingleResult<EventLogItem> result;
                    if (!c.GotFailedCheck)
                    {
                        result = await this.TryGetSingle<EventLogItem>("/api/eventlogitems/", e => e.EventType == typeof(CustomCheckFailed).Name);
                        failedCheckLogItem = result;
                        c.GotFailedCheck = result;
                        return false;
                    }

                    result = await this.TryGetSingle<EventLogItem>("/api/eventlogitems/", e => e.EventType == typeof(CustomCheckSucceeded).Name);
                    passingCheckLogItem = result;
                    return result;
                })
                .Run();

            Assert.AreEqual(Severity.Error, failedCheckLogItem.Severity, "Failed custom checks should be treated as error");
            Assert.IsTrue(failedCheckLogItem.RelatedTo.Any(item => item == "/customcheck/MyFailingCustomCheckId"));
            Assert.IsTrue(failedCheckLogItem.RelatedTo.Any(item => item.StartsWith($"/endpoint/{Conventions.EndpointNamingConvention(typeof(EndpointWithCustomChecks))}")));
            Assert.AreEqual(Severity.Info, passingCheckLogItem.Severity, "Passing custom checks should be treated as info");
            Assert.IsTrue(passingCheckLogItem.RelatedTo.Any(item => item == "/customcheck/MyPassingCustomCheckId"));
            Assert.IsTrue(passingCheckLogItem.RelatedTo.Any(item => item.StartsWith($"/endpoint/{Conventions.EndpointNamingConvention(typeof(EndpointWithCustomChecks))}")));
        }

        public class MyContext : ScenarioContext
        {
            public bool GotFailedCheck { get; set; }
        }

        public class EndpointWithCustomChecks : EndpointConfigurationBuilder
        {
            public EndpointWithCustomChecks()
            {
                EndpointSetup<DefaultServerWithAudit>(c => { c.ReportCustomChecksTo(Settings.DEFAULT_SERVICE_NAME, TimeSpan.FromSeconds(1)); });
            }

            class FailingCustomCheck : CustomCheck
            {
                public FailingCustomCheck()
                    : base("MyFailingCustomCheckId", "MyCategory")
                {
                }

                public override Task<CheckResult> PerformCheck()
                {
                    return CheckResult.Failed("Some reason");
                }
            }

            class PassingCustomCheck : CustomCheck
            {
                public PassingCustomCheck()
                    : base("MyPassingCustomCheckId", "MyCategory")
                {
                }

                public override Task<CheckResult> PerformCheck()
                {
                    return CheckResult.Pass;
                }
            }
        }
    }
}