namespace ServiceControl.AcceptanceTests.Legacy
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTests;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json.Linq;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;

    class RootControllerTests : AcceptanceTest
    {
        [Test]
        public async Task Should_gather_remote_data()
        {
            // Since we don't have an audit instance running in a test, use the primary instance
            // configuration URL just to ensure the JSON is combined correctly.
            const string localApiUrl = "http://localhost:33333/api";
            var serviceName = Guid.NewGuid().ToString("n");

            CustomizeHostBuilder = hostBuilder =>
            {
                hostBuilder.ConfigureServices((hostBuilderContext, services) =>
                {
                    services.AddSingleton(new Settings(serviceName)
                    {
                        RemoteInstances = new[]
                        {
                            new RemoteInstanceSetting { ApiUri = localApiUrl },
                            new RemoteInstanceSetting { ApiUri = localApiUrl }
                        }
                    });
                });
            };

            JArray config = null;

            var context = await Define<ScenarioContext>() // Don't need a context
                .Done(async c =>
                {
                    var result = await this.TryGet<JArray>("/api/configuration/remotes");
                    config = result.Item;
                    return result.HasResult;
                })
                .Run();

            Assert.IsNotNull(config);
            Assert.That(config.Count, Is.EqualTo(2));

            var config1 = config[0];
            var config2 = config[1];
            var config1Str = config1.ToString();
            var config2Str = config2.ToString();

            Assert.That(config1Str, Is.EqualTo(config2Str));

            Assert.That(config1["api_uri"].Value<string>(), Is.EqualTo(localApiUrl));
            Assert.That(config1["status"].Value<string>(), Is.EqualTo("online"));
            Assert.That(config1Str, Contains.Substring(serviceName));
        }
    }
}