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
            JsonObject environmentData = null;

            _ = await Define<Context>()
                .Done(async ctx =>
                {
                    var response = await this.GetRaw("/api/environment");

                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }

                    var body = await JsonNode.ParseAsync(await response.Content.ReadAsStreamAsync());
                    environmentData = body?["environment_data"]?.AsObject();

                    return environmentData is not null;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That((string)environmentData["Storage.Type"], Is.Not.Null.And.Not.Empty);
                Assert.That((string)environmentData["Storage.ServerVersion"], Is.Not.Null.And.Not.Empty);
                Assert.That((string)environmentData["Storage.FullTextSearch"], Is.AnyOf("Enabled", "Disabled"));
                Assert.That((string)environmentData["Host.Model"], Is.AnyOf("Container", "WindowsService", "Console"));
                Assert.That((string)environmentData["Host.ProcessorCount"], Is.Not.Null.And.Not.Empty);
            }
        }

        class Context : ScenarioContext;
    }
}
