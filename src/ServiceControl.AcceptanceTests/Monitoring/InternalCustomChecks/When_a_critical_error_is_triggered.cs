namespace ServiceControl.AcceptanceTests.Monitoring.InternalCustomChecks
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Contracts.CustomChecks;
    using EventLog;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Operations;

    [TestFixture]
    [NonParallelizable]
    class When_a_critical_error_is_triggered : AcceptanceTest
    {
        [Test]
        public async Task Service_control_is_not_killed_and_error_is_reported_via_custom_check()
        {
            CustomizeHostBuilder = builder =>
            {
                // Replace the production registration of the critical error custom check with a test version that has a shorter interval to trigger the critical error faster.
                var productionRegistration = builder.Services.Single(registration =>
                    registration.ServiceType == typeof(ICustomCheck) &&
                    registration.ImplementationType == typeof(CriticalErrorCustomCheck));

                builder.Services.Remove(productionRegistration);
                builder.Services.AddSingleton<ICustomCheck>(_ => new CriticalErrorCustomCheck(TimeSpan.FromSeconds(1)));
            };

            SetSettings = settings =>
            {
                settings.DisableHealthChecks = false;
            };
            EventLogItem entry = null;

            await Define<MyContext>()
                .Done(async c =>
                {
                    if (!c.CriticalErrorTriggered)
                    {
                        await this.Post<object>("/api/criticalerror/trigger?message=Failed");
                        c.CriticalErrorTriggered = true;
                    }

                    var result = await this.TryGetSingle<EventLogItem>("/api/eventlogitems/", e => e.EventType == nameof(CustomCheckFailed) && e.Description.Contains("ServiceControl Primary Instance"));
                    entry = result;
                    return result;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(entry.RelatedTo.Any(item => item == "/customcheck/ServiceControl Primary Instance"), Is.True, "Event log entry should be related to the Primary instance health Custom Check");
                Assert.That(entry.RelatedTo.Any(item => item.StartsWith("/endpoint/Particular.ServiceControl")), Is.True, "Event log entry should be related to the ServiceControl endpoint");
            }
        }

        public class MyContext : ScenarioContext
        {
            public bool CriticalErrorTriggered { get; set; }
        }
    }
}