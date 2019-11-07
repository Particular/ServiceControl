namespace NServiceBus.Metrics.AcceptanceTests
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::Newtonsoft.Json.Linq;
    using global::ServiceControl.Monitoring;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public abstract class ApiIntegrationTest : NServiceBusAcceptanceTest2
    {
        protected Bootstrapper Bootstrapper { get; set; }

        [SetUp]
        public void Setup()
        {
            httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));

            Bootstrapper = new Bootstrapper(Settings);
        }

        [TearDown]
        public async Task TearDown()
        {
            httpClient?.Dispose();
            await Bootstrapper.Stop().ConfigureAwait(false);
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