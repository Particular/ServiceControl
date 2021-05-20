namespace ServiceControl.AcceptanceTests.Monitoring.InternalCustomChecks
{
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Contracts.CustomChecks;
    using EventLog;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using Particular.ServiceControl;
    using ServiceControl.CustomChecks;
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

            public override Task<CheckResult> PerformCheck()
            {
                return Task.FromResult(CheckResult.Failed("Some reason"));
            }
        }

        [Test]
        public async Task Should_result_in_a_custom_check_failed_event()
        {
            var allServiceControlAndInternalCustomChecksTypes = typeof(InternalCustomChecks).Assembly.GetTypes()
                .Union(typeof(Bootstrapper).Assembly.GetTypes()).ToArray();
            var allTypesExcludingBuiltInCustomChecks = allServiceControlAndInternalCustomChecksTypes.Where(t => !t.GetInterfaces().Contains(typeof(ICustomCheck)));
            var customChecksUnderTest = new[] { typeof(FailingCustomCheck) };

            CustomConfiguration = config =>
            {
                config.EnableFeature<InternalCustomChecks>();
                config.TypesToIncludeInScan(allTypesExcludingBuiltInCustomChecks.Concat(customChecksUnderTest));
            };
            EventLogItem entry = null;

            await Define<MyContext>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EventLogItem>("/api/eventlogitems/", e => e.EventType == nameof(Contracts.CustomChecks.CustomCheckFailed));
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