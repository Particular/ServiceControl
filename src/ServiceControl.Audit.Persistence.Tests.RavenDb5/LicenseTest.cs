﻿namespace ServiceControl.Audit.Persistence.Tests.RavenDb5
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using NUnit.Framework;

    class LicenseTest : PersistenceTestFixture
    {
        [Test]
        public async Task EnsureLicenseIsValid()
        {
            using (var http = new HttpClient())
            using (var response = await http.GetAsync(configuration.DocumentStore.Urls.First() + "/license/status"))
            {
                response.EnsureSuccessStatusCode();

                var jsonText = await response.Content.ReadAsStringAsync();
                var details = System.Text.Json.JsonSerializer.Deserialize<LicenseDetails>(jsonText);

                Assert.That(details.Id, Is.EqualTo("64c6a174-3f3a-4e7d-ac5d-b3eedd801460"));
                Assert.That(details.LicensedTo, Is.EqualTo("ParticularNservicebus (Israel)"));
                Assert.That(details.Status, Is.EqualTo("Commercial"));
                Assert.That(details.Expired, Is.False);
                Assert.That(details.Type, Is.EqualTo("Professional"));
                Assert.That(DateTime.UtcNow.AddDays(14) < details.Expiration, "The RavenDB license expires in less than 2 weeks. Contact RavenDB for the new license.");
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
