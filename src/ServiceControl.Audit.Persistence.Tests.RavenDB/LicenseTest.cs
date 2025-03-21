namespace ServiceControl.Audit.Persistence.Tests.RavenDB
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;

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

                    Assert.Multiple(() =>
                    {
                        Assert.That(details.Id, Is.EqualTo("64c6a174-3f3a-4e7d-ac5d-b3eedd801460"));
                        Assert.That(details.LicensedTo, Is.EqualTo("ParticularNservicebus (Israel)"));
                        Assert.That(details.Status, Is.EqualTo("Commercial"));
                        Assert.That(details.Expired, Is.False);
                        Assert.That(details.Type, Is.EqualTo("Professional"));
                        Assert.That(DateTime.UtcNow.AddDays(7), Is.LessThan(details.Expiration), $"The RavenDB license expires {details.Expiration} which is less than one week. Contact RavenDB at <sales@ravendb.net> for the new license.");
                    });
                }
            }
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
