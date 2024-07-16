namespace ServiceControl.AcceptanceTests.Legacy
{
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTests;
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
            string serviceName = null, baseAddress = null;

            SetSettings = settings =>
            {
                settings.RemoteInstances =
                [
                    new RemoteInstanceSetting(settings.RootUrl),
                    new RemoteInstanceSetting(settings.RootUrl)
                ];
                serviceName = settings.InstanceName;
                baseAddress = settings.RootUrl;
            };

            JsonArray config = null;

            var context = await Define<ScenarioContext>() // Don't need a context
                .Done(async c =>
                {
                    var result = await this.TryGet<JsonArray>("/api/configuration/remotes");
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

            Assert.That(config1["api_uri"].GetValue<string>(), Is.EqualTo(baseAddress));
            Assert.That(config1["status"].GetValue<string>(), Is.EqualTo("online"));
            Assert.That(config1["version"].GetValue<string>(), Does.Match(@"^\d+\.\d+\.\d+(-[\w\d\.\-]+)?$"));
            Assert.That(config1Str, Contains.Substring(serviceName));
        }
    }
}