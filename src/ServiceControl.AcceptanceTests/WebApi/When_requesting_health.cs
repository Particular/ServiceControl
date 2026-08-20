namespace ServiceControl.AcceptanceTests.WebApi
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    class When_requesting_health : AcceptanceTest
    {
        [Test]
        public async Task Should_report_liveness_and_readiness_as_json()
        {
            await Define<ScenarioContext>()
                .Done(async c =>
                {
                    var liveness = await this.GetRaw("/health");
                    var readiness = await this.GetRaw("/health/ready");

                    using (Assert.EnterMultipleScope())
                    {
                        // The container health check binary rejects anything that is not non-empty
                        // JSON, and the Dockerfile probes /health in every mode.
                        Assert.That(liveness.IsSuccessStatusCode, Is.True);
                        Assert.That(liveness.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
                        Assert.That(await liveness.Content.ReadAsStringAsync(), Does.Contain("Healthy"));

                        Assert.That(readiness.IsSuccessStatusCode, Is.True);
                        Assert.That(await readiness.Content.ReadAsStringAsync(), Does.Contain("error-ingestion"));
                    }

                    return true;
                })
                .Run();
        }
    }
}
