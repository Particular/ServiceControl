namespace ServiceBus.Management.AcceptanceTests.CustomChecks
{
    using System;
    using System.Linq;
    using Contexts;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.EventLog;
    using ServiceControl.Plugin.CustomChecks;

    [TestFixture]
    public class When_a_periodic_custom_check_fails : AcceptanceTest
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

            Assert.AreEqual(Severity.Error, entry.Severity, "Failed custom checks should be treated as info");
            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/customcheck/MyCustomCheckId"));
            Assert.IsTrue(entry.RelatedTo.Any(item => item.StartsWith("/endpoint/CustomChecks.EndpointWithFailingCustomCheck")));
           
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

            class FailingCustomCheck : PeriodicCheck
            {
                public FailingCustomCheck() : base("MyCustomCheckId", "MyCategory", TimeSpan.FromHours(1))
                {
                }

                public override CheckResult PerformCheck()
                {
                    return CheckResult.Failed("Some reason");
                }
            }
        }
    }
}