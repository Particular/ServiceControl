﻿namespace ServiceBus.Management.AcceptanceTests.Infrastructure.CustomChecks
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Plugin.CustomChecks.Messages;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    [TestFixture]
    class When_a_custom_check_triggered : AcceptanceTest
    {
        [Test]
        public async Task Should_result_in_report_events()
        {
            CustomConfiguration = ConfigureWaitingForEventSpyToSubscribe;

            var context = await Define<MyContext>()
                .WithEndpoint<EventSpy>(b => b.When(async (s, c) =>
                {
                    await s.Subscribe<ReportCustomCheckResult>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.EventSpySubscribed.TrySetResult(true);
                    }
                }))
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

        static void ConfigureWaitingForEventSpyToSubscribe(EndpointConfiguration config)
        {
            config.OnEndpointSubscribed<MyContext>((s, ctx) =>
            {
                if (s.SubscriberReturnAddress.IndexOf(Conventions.EndpointNamingConvention(typeof(EventSpy)), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ctx.EventSpySubscribed.TrySetResult(true);
                }
            });
        }

        public class MyContext : ScenarioContext
        {
            public ConcurrentBag<CustomCheckData> Messages { get; set; } = new ConcurrentBag<CustomCheckData>();
            public TaskCompletionSource<bool> EventSpySubscribed { get; set; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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

            public override string ToString() => new { CustomCheckId, Category, HasFailed, FailureReason }.ToString();

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
                }, metadata => { metadata.RegisterPublisherFor<ReportCustomCheckResult>(Settings.DEFAULT_SERVICE_NAME); });
            }

            public class EventHandler : IHandleMessages<ReportCustomCheckResult>
            {
                public MyContext TestContext { get; set; }

                public Task Handle(ReportCustomCheckResult message, IMessageHandlerContext context)
                {
                    if (!TestContext.HasNativePubSubSupport || context.MessageHeaders[Headers.OriginatingEndpoint] == Settings.DEFAULT_SERVICE_NAME)
                    {
                        TestContext.Messages.Add(new CustomCheckData(message));
                    }
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
                public FailingCustomCheck(MyContext testContext)
                    : base("MyFailingCustomCheckId", "MyCategory")
                {
                    this.testContext = testContext;
                }

                public override async Task<CheckResult> PerformCheck()
                {
                    await testContext.EventSpySubscribed.Task;
                    return CheckResult.Failed("Some reason");
                }

                MyContext testContext;
            }

            class PassingCustomCheck : CustomCheck
            {
                public PassingCustomCheck(MyContext testContext)
                    : base("MyPassingCustomCheckId", "MyCategory")
                {
                    this.testContext = testContext;
                }

                public override async Task<CheckResult> PerformCheck()
                {
                    await testContext.EventSpySubscribed.Task;
                    return CheckResult.Pass;
                }

                MyContext testContext;
            }
        }
    }
}