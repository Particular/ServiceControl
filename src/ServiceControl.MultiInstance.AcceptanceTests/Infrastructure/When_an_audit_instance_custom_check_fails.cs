﻿namespace ServiceControl.MultiInstance.AcceptanceTests.Infrastructure
{
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTests;
    using EventLog;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using TestSupport;

    [TestFixture]
    class When_an_audit_instance_custom_check_fails : AcceptanceTest
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
            CustomServiceControlSettings = settings =>
            {
                settings.DisableInternalHealthChecks = false;
            };

            CustomizeAuditHostBuilder = builder =>
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