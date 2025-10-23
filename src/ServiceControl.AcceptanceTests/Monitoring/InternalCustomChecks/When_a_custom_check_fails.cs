namespace ServiceControl.AcceptanceTests.Monitoring.InternalCustomChecks
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EventLog;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using CustomCheck = NServiceBus.CustomChecks.CustomCheck;

    [TestFixture]
    class When_a_custom_check_fails : AcceptanceTest
    {
        public class FailingCustomCheck : CustomCheck
        {
            public FailingCustomCheck()
                : base("MyCustomCheckId", "MyCategory")
            {
            }

            public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(CheckResult.Failed("Some reason"));
            }
        }

        [Test]
        public async Task Should_result_in_a_custom_check_failed_event()
        {
            SetSettings = settings =>
            {
                settings.ServiceControl.DisableHealthChecks = false;
            };

            CustomizeHostBuilder = builder =>
            {
                builder.Services.AddTransient<ICustomCheck, FailingCustomCheck>();
            };

            EventLogItem entry = null;

            await Define<MyContext>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EventLogItem>("/api/eventlogitems/", e => e.EventType == nameof(Contracts.CustomChecks.CustomCheckFailed) && e.Description.Contains("MyCustomCheckId"));
                    entry = result;
                    return result;
                })
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(entry.Severity, Is.EqualTo(Severity.Error), "Failed custom checks should be treated as error");
                Assert.That(entry.RelatedTo.Any(item => item == "/customcheck/MyCustomCheckId"), Is.True);
                Assert.That(entry.RelatedTo.Any(item => item.StartsWith("/endpoint/Particular.ServiceControl")), Is.True);
            });
        }

        public class MyContext : ScenarioContext
        {
        }
    }
}