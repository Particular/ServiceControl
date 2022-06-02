namespace ServiceControl.AcceptanceTests.Monitoring.InternalCustomChecks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Contracts.CustomChecks;
    using EventLog;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NUnit.Framework;
    using Operations;
    using ServiceControl.CustomChecks;
    using ServiceControl.CustomChecks.Internal;
    using ICustomCheck = NServiceBus.CustomChecks.ICustomCheck;

    [TestFixture]
    [RunOnAllTransports]
    class When_a_critical_error_is_triggered : AcceptanceTest
    {
        static Type[] allServiceControlTypes = typeof(InternalCustomChecks).Assembly.GetTypes();
        static IEnumerable<Type> allTypesExcludingBuiltInCustomChecks = allServiceControlTypes.Where(t => !t.GetInterfaces().Contains(typeof(ICustomCheck)));
        static Type[] customChecksUnderTest = { typeof(CriticalErrorCustomCheck) };
        static IEnumerable<Type> typesToScan = allTypesExcludingBuiltInCustomChecks.Concat(customChecksUnderTest);

        [Test]
        public async Task Service_control_is_not_killed_and_error_is_reported_via_custom_check()
        {
            SetSettings = settings =>
            {
                settings.DisableHealthChecks = false;
            };
            EventLogItem entry = null;

            var criticalErrorTriggered = false;

            await Define<MyContext>()
                .Done(async c =>
                {
                    if (!criticalErrorTriggered)
                    {
                        await this.Post<object>("/api/criticalerror/trigger?message=Failed");
                        criticalErrorTriggered = true;
                        return false;
                    }

                    var result = await this.TryGetSingle<EventLogItem>("/api/eventlogitems/", e => e.EventType == nameof(CustomCheckFailed) && e.Description.Contains("ServiceControl Primary Instance"));
                    entry = result;
                    return result;
                })
                .Run();



            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/customcheck/ServiceControl Primary Instance"), "Event log entry should be related to the Primary instance health Custom Check");
            Assert.IsTrue(entry.RelatedTo.Any(item => item.StartsWith("/endpoint/Particular.ServiceControl")), "Event log entry should be related to the ServiceControl endpoint");
        }

        public class MyContext : ScenarioContext
        {
        }
    }
}