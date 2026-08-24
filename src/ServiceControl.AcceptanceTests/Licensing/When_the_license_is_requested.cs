namespace ServiceControl.AcceptanceTests.Licensing
{
    using System.Net;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Licensing;

    class When_the_license_is_requested : AcceptanceTest
    {
        [Test]
        public async Task Should_report_the_instance_and_where_to_extend_the_trial()
        {
            LicenseInfo license = null;

            await Define<Context>()
                .Done(async _ =>
                {
                    var result = await this.TryGet<LicenseInfo>($"/api/license?refresh=true&clientName={ClientName}");
                    license = result.Item;
                    return result.HasResult;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(license.InstanceName, Is.EqualTo(Settings.InstanceName),
                    "ServicePulse labels the license page with the instance it is talking to");

                Assert.That(license.LicenseExtensionUrl, Does.StartWith("https://particular.net/extend-your-trial"),
                    "With no MassTransit connector reporting in, the license page offers the trial extension rather than the connector link");

                Assert.That(license.LicenseExtensionUrl, Does.Contain($"p={ClientName}"),
                    "The link has to carry the caller through, or Particular cannot tell which product the request came from");
            }
        }

        [Test]
        public async Task Should_reject_a_request_that_names_no_client()
        {
            HttpStatusCode status = default;

            await Define<Context>()
                .Done(async _ =>
                {
                    using var response = await this.GetRaw("/api/license");
                    status = response.StatusCode;
                    return true;
                })
                .Run();

            Assert.That(status, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        const string ClientName = "servicepulse";

        class Context : ScenarioContext;
    }
}
