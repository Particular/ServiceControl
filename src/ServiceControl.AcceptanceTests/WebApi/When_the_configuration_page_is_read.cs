namespace ServiceControl.AcceptanceTests.WebApi
{
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Particular.LicensingComponent.Contracts;

    class When_the_configuration_page_is_read : AcceptanceTest
    {
        [Test]
        public async Task Should_report_the_instance_the_same_way_from_both_of_its_routes()
        {
            string configuration = null;
            string instanceInfo = null;

            await Define<Context>()
                .Done(async _ =>
                {
                    configuration = await Body("/api/configuration");
                    instanceInfo = await Body("/api/instance-info");

                    return true;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(configuration, Is.EqualTo(instanceInfo),
                    "Both routes are the same action, and nothing would notice if they drifted apart");

                Assert.That(configuration, Does.Contain(Settings.InstanceName),
                    "The configuration page names the instance it is describing");
            }
        }

        [Test]
        public async Task Should_accept_licensed_endpoint_details_and_report_none_without_the_licence_for_them()
        {
            HttpStatusCode upload = default;
            HttpStatusCode read = default;
            string reported = null;

            await Define<Context>()
                .Done(async _ =>
                {
                    using var uploaded = await HttpClient.PostAsync("/api/license/detailsUpload", Compressed(new LicensedEndpointDetails
                    {
                        LicenseId = "a-licence-this-instance-does-not-hold",
                        ServiceEndDate = "2027-01-01"
                    }));

                    upload = uploaded.StatusCode;

                    using var details = await this.GetRaw("/api/license/details");

                    read = details.StatusCode;
                    reported = await details.Content.ReadAsStringAsync();

                    return true;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(upload, Is.EqualTo(HttpStatusCode.OK),
                    "The upload is how a customer supplies the endpoint details their licence covers");

                Assert.That(read, Is.EqualTo(HttpStatusCode.NoContent),
                    "Details are only reported back on an Endpoint Size licence carrying endpoint metadata, and the page hides the section on this answer");

                Assert.That(reported, Is.Empty,
                    "A 204 carries no body, so nothing here is for ServicePulse to parse");
            }
        }

        static MultipartFormDataContent Compressed(LicensedEndpointDetails details)
        {
            var buffer = new MemoryStream();

            using (var brotli = new BrotliStream(buffer, CompressionMode.Compress, leaveOpen: true))
            {
                JsonSerializer.Serialize(brotli, details);
            }

            var file = new ByteArrayContent(buffer.ToArray());

            return new MultipartFormDataContent { { file, "file", "details.json.br" } };
        }

        async Task<string> Body(string url)
        {
            using var response = await this.GetRaw(url);

            return await response.Content.ReadAsStringAsync();
        }

        class Context : ScenarioContext;
    }
}
