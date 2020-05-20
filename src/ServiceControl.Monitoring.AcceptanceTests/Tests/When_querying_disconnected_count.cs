namespace ServiceControl.Monitoring.AcceptanceTests.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NLog.Fluent;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.AcceptanceTesting;
    using ServiceControl.Monitoring.AcceptanceTests.TestSupport.EndpointTemplates;

    class When_querying_disconnected_count : AcceptanceTest
    {
        [Test]
        public async Task Should_report_via_http()
        {
            TestContext context = null;

            await Define<TestContext>(ctx => context = ctx)
                .WithEndpoint<MonitoredEndpoint>(b => b.ToCreateInstance(endpointConfig => Task.FromResult(endpointConfig), async endpointConfig =>
                {
                    context.FirstInstance = await Endpoint.Start(endpointConfig);
                    return context.FirstInstance;
                }))
                .WithEndpoint<MonitoredEndpoint>(b => b.ToCreateInstance(endpointConfig => Task.FromResult(endpointConfig), async endpointConfig =>
                {
                    context.SecondInstance = await Endpoint.Start(endpointConfig);
                    return context.SecondInstance;
                }))
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
                        Log.Info(result.ReasonPhrase);
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

            Assert.AreEqual(0, context.AfterAllStartedCount, "Disconnected count after all endpoints started");
            Assert.AreEqual(1, context.AfterFirstStoppedCount, "Disconnected count after first endpoint stopped");
            Assert.AreEqual(2, context.AfterSecondStoppedCount, "Disconnected count after both endpoints stopped");
        }

        class MonitoredEndpoint : EndpointConfigurationBuilder
        {
            public MonitoredEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableMetrics().SendMetricDataToServiceControl(Settings.DEFAULT_ENDPOINT_NAME, TimeSpan.FromSeconds(1));
                });
            }
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
