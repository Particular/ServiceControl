namespace ServiceControl.Audit.AcceptanceTests
{
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    class When_environment_data_is_requested : AcceptanceTest
    {
        [Test]
        public async Task Should_serve_host_and_storage_facts()
        {
            JsonObject body = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var response = await this.GetRaw("/api/environment");

                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }

                    body = (await JsonNode.ParseAsync(await response.Content.ReadAsStreamAsync()))?.AsObject();

                    return body?["environment_data"] is not null;
                })
                .Run();

            var environmentData = body["environment_data"].AsObject();
            var storageIdentity = body["storage_identity"]?.AsObject();

            using (Assert.EnterMultipleScope())
            {
                Assert.That((string)environmentData["Storage.Type"], Is.Not.Null.And.Not.Empty);
                Assert.That((string)environmentData["Storage.ServerVersion"], Is.Not.Null.And.Not.Empty);
                Assert.That((string)environmentData["Storage.FullTextSearch"], Is.AnyOf("Enabled", "Disabled"));
                Assert.That((string)environmentData["Host.Model"], Is.AnyOf("Container", "WindowsService", "Console"));
                Assert.That((string)environmentData["Host.ProcessorCount"], Is.Not.Null.And.Not.Empty);
                Assert.That((string)body["machine_id_hash"], Is.Not.Null.And.Not.Empty);
                Assert.That(storageIdentity, Is.Not.Null);
                Assert.That((string)storageIdentity["engine"], Is.Not.Null.And.Not.Empty);
                Assert.That((string)storageIdentity["server_hash"], Has.Length.EqualTo(64));
                Assert.That((string)storageIdentity["database_hash"], Has.Length.EqualTo(64));
            }
        }

        class Context : ScenarioContext;
    }
}
