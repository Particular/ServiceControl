namespace ServiceControl.Audit.UnitTests.Infrastructure.WebApi
{
    using System.Text.Json;
    using Audit.Infrastructure.WebApi;
    using Audit.Infrastructure.Settings;
    using NUnit.Framework;

    [TestFixture]
    class RootControllerConfigurationTests
    {
        const string RavenDbAlias = "ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb5";

        [Test]
        public void Reports_the_manifest_name_of_the_storage_type_and_no_persistence_section()
        {
            var controller = new RootController(new Settings(storageType: RavenDbAlias));

            var json = JsonSerializer.SerializeToNode(controller.Config().Value, SerializerOptions.Default).AsObject();

            Assert.Multiple(() =>
            {
                Assert.That((string)json["storage"]["storage_type"], Is.EqualTo("RavenDB"));
                Assert.That(json.ContainsKey("persistence"), Is.False);
            });
        }
    }
}
