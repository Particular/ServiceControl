namespace ServiceControl.AcceptanceTests.Monitoring.InternalCustomChecks
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EventLog;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.CustomChecks.Internal;

    [TestFixture]
    class When_a_custom_check_fails : AcceptanceTest
    {
        public class FailingCustomCheck : CustomCheck
        {
            public FailingCustomCheck()
                : base("MyCustomCheckId", "MyCategory")
            {
            }

            public override Task<CheckResult> PerformCheck(CancellationToken token = default)
            {
                return Task.FromResult(CheckResult.Failed("Some reason"));
            }
        }

        [Test]
        public async Task Should_result_in_a_custom_check_failed_event()
        {
            SetSettings = settings =>
            {
                settings.DisableHealthChecks = false;
            };

            CustomizeHostBuilder = builder =>
            {
                builder.ConfigureServices((context, collection) =>
                {
                    collection.AddTransient<ICustomCheck, FailingCustomCheck>();
                });
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

            Assert.AreEqual(Severity.Error, entry.Severity, "Failed custom checks should be treated as error");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/customcheck/MyCustomCheckId"));
            Assert.IsTrue(entry.RelatedTo.Any(item => item.StartsWith("/endpoint/Particular.ServiceControl")));
        }

        public class MyContext : ScenarioContext
        {
        }
    }
}