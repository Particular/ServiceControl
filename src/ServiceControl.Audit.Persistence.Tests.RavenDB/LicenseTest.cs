namespace ServiceControl.Audit.Persistence.Tests.RavenDB
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Persistence.RavenDB;

    [NonParallelizable]
    class LicenseTest : PersistenceTestFixture
    {
        [Test]
        public async Task EnsureLicenseIsValid()
        {
            var ravenUrl = configuration.DocumentStore.Urls.First();

            using (var http = new HttpClient())
            {
                // Ensure the license is activated before checking its details, otherwise it might say unlicensed
                using (var response = await http.PostAsync(ravenUrl + "/admin/cluster/bootstrap", new StringContent(string.Empty, Encoding.UTF8, "application/json")))
                {
                    response.EnsureSuccessStatusCode();
                }

                // Get the license details from the server
                using (var response = await http.GetAsync(ravenUrl + "/license/status"))
                {
                    response.EnsureSuccessStatusCode();

                    var jsonText = await response.Content.ReadAsStringAsync();
                    var details = System.Text.Json.JsonSerializer.Deserialize<LicenseDetails>(jsonText);

                    using (Assert.EnterMultipleScope())
                    {
                        Assert.That(details.Id, Is.EqualTo("64c6a174-3f3a-4e7d-ac5d-b3eedd801460"));
                        Assert.That(details.LicensedTo, Is.EqualTo("NserviceBus Ltd"));
                        Assert.That(details.Status, Is.EqualTo("Commercial"));
                        Assert.That(details.Expired, Is.False);
                        Assert.That(details.Type, Is.EqualTo("Professional"));
                        Assert.That(DateTime.UtcNow.AddDays(7), Is.LessThan(details.Expiration), $"The RavenDB license expires {details.Expiration} which is less than one week. Contact RavenDB at <sales@ravendb.net> for the new license.");
                    }
                }
            }
        }

        // LicenseStatusCheck deserializes the response into its own type. A renamed property leaves the default value in place,
        // so the check waits for the timeout or reports no problem, which is why the names are asserted on the raw response.
        [Test]
        public async Task License_status_should_contain_the_properties_read_by_the_license_check()
        {
            var ravenUrl = configuration.DocumentStore.Urls.First();

            using var http = new HttpClient();
            var jsonText = await http.GetStringAsync(ravenUrl + "/license/status", TestTimeoutCancellationToken);

            using var document = JsonDocument.Parse(jsonText);
            var license = document.RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(license.TryGetProperty("Id", out var id), Is.True, "Id is missing from the response");
                Assert.That(id.ValueKind, Is.EqualTo(JsonValueKind.String));
                Assert.That(license.TryGetProperty("LicensedTo", out var licensedTo), Is.True, "LicensedTo is missing from the response");
                Assert.That(licensedTo.ValueKind, Is.EqualTo(JsonValueKind.String));
                Assert.That(license.TryGetProperty("Status", out var status), Is.True, "Status is missing from the response");
                Assert.That(status.ValueKind, Is.EqualTo(JsonValueKind.String));
                Assert.That(license.TryGetProperty("Expired", out var expired), Is.True, "Expired is missing from the response");
                Assert.That(expired.ValueKind, Is.AnyOf(JsonValueKind.True, JsonValueKind.False));
            }
        }

        [Test]
        public async Task License_status_check_should_accept_the_license_of_the_server()
        {
            await LicenseStatusCheck.WaitForLicenseOrThrow(configuration.DocumentStore, TestTimeoutCancellationToken);
        }

        class LicenseDetails
        {
            public string Id { get; set; }
            public string LicensedTo { get; set; }
            public string Status { get; set; }
            public bool Expired { get; set; }
            public string Type { get; set; }
            public DateTime Expiration { get; set; }
        }
    }
}
