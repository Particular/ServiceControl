namespace ServiceControl.Monitoring.SmokeTests.AzureStorageQueues.Tests
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Newtonsoft.Json.Linq;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    public abstract class ApiIntegrationTest : NServiceBusAcceptanceTest
    {
        [SetUp]
        public void Setup()
        {
            httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        [TearDown]
        public void TearDown()
        {
            httpClient?.Dispose();
        }

        protected bool MetricReported(string name, out JToken metric, Context context)
        {
            var jsonResponse = GetString(MonitoredEndpointsUrl);
            var response = JArray.Parse(jsonResponse);

            metric = response.Count > 0 ? response[0]["metrics"][name] : null;

            context.MetricsReport = jsonResponse;

            return metric != null && metric["average"].Value<double>() > 0d;
        }

        string GetString(string url)
        {
            return httpClient.GetStringAsync(url).GetAwaiter().GetResult();
        }

        string MonitoredEndpointsUrl = "http://localhost:1234/monitored-endpoints?history=1";
        HttpClient httpClient;

        protected class Context : ScenarioContext
        {
            public string MetricsReport { get; set; }
        }
        public class SampleMessage : IMessage
        {
        }
    }
}