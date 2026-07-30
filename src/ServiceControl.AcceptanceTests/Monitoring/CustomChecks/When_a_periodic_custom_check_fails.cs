namespace ServiceControl.AcceptanceTests.Monitoring.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Contracts.CustomChecks;
    using EventLog;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    [TestFixture]
    class When_a_periodic_custom_check_fails : AcceptanceTest
    {
        [Test]
        public async Task Should_result_in_a_custom_check_failed_event()
        {
            EventLogItem entry = null;

            await Define<MyContext>()
                .WithEndpoint<WithCustomCheck>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EventLogItem>("/api/eventlogitems/", e => e.EventType == nameof(CustomCheckFailed));
                    entry = result;
                    return result;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(entry.Severity, Is.EqualTo(Severity.Error), "Failed custom checks should be treated as error");
                Assert.That(entry.RelatedTo.Any(item => item == "/customcheck/MyCustomCheckId"), Is.True);
                Assert.That(entry.RelatedTo.Any(item => item.StartsWith($"/endpoint/{Conventions.EndpointNamingConvention(typeof(WithCustomCheck))}")), Is.True);
            }
        }

        public class MyContext : ScenarioContext;

        public class WithCustomCheck : EndpointConfigurationBuilder
        {
            public WithCustomCheck() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.ReportCustomChecksTo(Settings.DEFAULT_INSTANCE_NAME, TimeSpan.FromSeconds(1)); });

            class FailingCustomCheck()
                : NServiceBus.CustomChecks.CustomCheck("MyCustomCheckId", "MyCategory", TimeSpan.FromSeconds(5))
            {
                public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
                {
                    if (executed)
                    {
                        return Task.FromResult(CheckResult.Failed("Some reason"));
                    }

                    executed = true;

                    return Task.FromResult(CheckResult.Pass);
                }

                bool executed;
            }
        }
    }
}
