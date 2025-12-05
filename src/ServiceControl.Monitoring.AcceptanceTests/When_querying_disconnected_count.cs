namespace ServiceControl.Monitoring.AcceptanceTests.Tests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.AcceptanceTesting;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_querying_disconnected_count : AcceptanceTest
    {
        [Test]
        public async Task Should_report_via_http()
        {
            TestContext context = null;

            SetSettings = settings =>
            {
                settings.EndpointUptimeGracePeriod = TimeSpan.FromSeconds(1);
            };

            await Define<TestContext>(ctx => context = ctx)
                .WithEndpoint<MonitoredEndpoint>(b =>
                    b.CustomConfig(c => c.EnableMetrics().SendMetricDataToServiceControl(Settings.DEFAULT_INSTANCE_NAME, TimeSpan.FromMilliseconds(200), "First")))
                .WithEndpoint<MonitoredEndpoint>(b =>
                    b.CustomConfig(c => c.EnableMetrics().SendMetricDataToServiceControl(Settings.DEFAULT_INSTANCE_NAME, TimeSpan.FromMilliseconds(200), "Second")))
                .WithServiceResolve((provider, _) =>
                {
                    var endpointName = Conventions.EndpointNamingConvention(typeof(MonitoredEndpoint));
                    context.FirstInstance = provider.GetRequiredKeyedService<IEndpointInstance>($"{endpointName}0");
                    context.SecondInstance = provider.GetRequiredKeyedService<IEndpointInstance>($"{endpointName}0");
                    return Task.CompletedTask;
                })
                .Done(async c =>
                {
                    if (!c.WaitedInitial2Seconds)
                    {
                        await Task.Delay(2000);
                        c.WaitedInitial2Seconds = true;
                    }

                    var result = await this.GetRaw("/monitored-endpoints/disconnected");

                    if (!result.IsSuccessStatusCode)
                    {
                        await Task.Delay(1000);
                        return false;
                    }

                    var bodyContent = await result.Content.ReadAsStringAsync();
                    var disconnectedCount = int.Parse(bodyContent);

                    if (!c.StoppedFirstInstance)
                    {
                        c.AfterAllStartedCount = disconnectedCount;
                        await c.FirstInstance.Stop();
                        c.StoppedFirstInstance = true;
                        await Task.Delay(2000);
                        return false;
                    }

                    if (!c.StoppedSecondInstance)
                    {
                        c.AfterFirstStoppedCount = disconnectedCount;
                        await c.SecondInstance.Stop();
                        c.StoppedSecondInstance = true;
                        await Task.Delay(2000);
                        return false;
                    }

                    c.AfterSecondStoppedCount = disconnectedCount;
                    return true;
                })
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(context.AfterAllStartedCount, Is.EqualTo(0), "Disconnected count after all endpoints started");
                Assert.That(context.AfterFirstStoppedCount, Is.EqualTo(0), "Disconnected count after first endpoint stopped");
                Assert.That(context.AfterSecondStoppedCount, Is.EqualTo(1), "Disconnected count after both endpoints stopped");
            });
        }

        class MonitoredEndpoint : EndpointConfigurationBuilder
        {
            public MonitoredEndpoint() => EndpointSetup<DefaultServerWithoutAudit>();
        }

        class TestContext : ScenarioContext
        {
            public bool WaitedInitial2Seconds { get; set; }
            public IEndpointInstance FirstInstance { get; set; }
            public bool StoppedFirstInstance { get; set; }
            public IEndpointInstance SecondInstance { get; set; }
            public bool StoppedSecondInstance { get; set; }
            public int AfterAllStartedCount { get; set; } = int.MinValue; //So we know if there is a logical failure, not a zero was returned
            public int AfterFirstStoppedCount { get; set; } = int.MinValue;
            public int AfterSecondStoppedCount { get; set; } = int.MinValue;
        }
    }
}
