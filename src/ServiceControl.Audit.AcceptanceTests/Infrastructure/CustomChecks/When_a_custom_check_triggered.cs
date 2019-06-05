namespace ServiceBus.Management.AcceptanceTests.Infrastructure.CustomChecks
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Plugin.CustomChecks.Messages;

    [TestFixture]
    class When_a_custom_check_triggered : AcceptanceTest
    {
        [Test]
        public async Task Should_result_in_report_events()
        {
            var context = await Define<MyContext>()
                .WithEndpoint<EventSpy>(b => b.When(s => s.Subscribe<ReportCustomCheckResult>()))
                .WithEndpoint<EndpointWithCustomChecks>()
                .Done(c => c.Messages.Count == 2)
                .Run();

            var expected = new[]
            {
                new CustomCheckData {Category = "MyCategory", CustomCheckId = "MyFailingCustomCheckId", FailureReason = "Some reason", HasFailed = true},
                new CustomCheckData {Category = "MyCategory", CustomCheckId = "MyPassingCustomCheckId", FailureReason = null, HasFailed = false}
            };
            CollectionAssert.AreEquivalent(expected, context.Messages);
        }

        public class MyContext : ScenarioContext
        {
            public ConcurrentBag<CustomCheckData> Messages { get; set; } = new ConcurrentBag<CustomCheckData>();
        }

        public class CustomCheckData
        {
            public CustomCheckData()
            {
            }

            public CustomCheckData(ReportCustomCheckResult message)
            {
                CustomCheckId = message.CustomCheckId;
                Category = message.Category;
                HasFailed = message.HasFailed;
                FailureReason = message.FailureReason;
            }

            public string CustomCheckId { get; set; }
            public string Category { get; set; }
            public bool HasFailed { get; set; }
            public string FailureReason { get; set; }

            protected bool Equals(CustomCheckData other)
            {
                return string.Equals(CustomCheckId, other.CustomCheckId) && string.Equals(Category, other.Category) && HasFailed == other.HasFailed && string.Equals(FailureReason, other.FailureReason);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }

                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                if (obj.GetType() != GetType())
                {
                    return false;
                }

                return Equals((CustomCheckData)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (CustomCheckId != null ? CustomCheckId.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Category != null ? Category.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ HasFailed.GetHashCode();
                    hashCode = (hashCode * 397) ^ (FailureReason != null ? FailureReason.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        public class EventSpy : EndpointConfigurationBuilder
        {
            public EventSpy()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.Conventions().DefiningCommandsAs(t => typeof(ICommand).IsAssignableFrom(t) && t != typeof(ReportCustomCheckResult));
                    c.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || t == typeof(ReportCustomCheckResult));
                }, metadata =>
                {
                    metadata.RegisterPublisherFor<ReportCustomCheckResult>(Settings.DEFAULT_SERVICE_NAME);
                });
            }

            public class EventHandler : IHandleMessages<ReportCustomCheckResult>
            {
                public MyContext TestContext { get; set; }

                public Task Handle(ReportCustomCheckResult message, IMessageHandlerContext context)
                {
                    TestContext.Messages.Add(new CustomCheckData(message));
                    return Task.CompletedTask;
                }
            }
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