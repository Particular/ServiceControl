namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    class EFEnvironmentDataTests : EFPersistenceTestFixture
    {
        [Test]
        public async Task Serves_storage_facts_for_an_empty_schema()
        {
            var data = new Dictionary<string, string>();

            foreach (var datum in ServiceProvider.GetRequiredService<IEnvironmentDataProvider>().GetData())
            {
                data[datum.Key] = await datum.ReadValue(TestTimeoutCancellationToken);
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(data, Does.ContainKey("Storage.Type").WithValue(PersisterName));
                Assert.That(data, Does.ContainKey("Storage.HostingSource").WithValue("Probe"));
                Assert.That(int.TryParse(data["Storage.ServerVersion"], NumberStyles.Integer, CultureInfo.InvariantCulture, out _), Is.True, data["Storage.ServerVersion"]);
                Assert.That(data["Storage.ServerEdition"], Is.AnyOf("Express", "Standard", "Enterprise", "NotApplicable"));
                Assert.That(data["Storage.ServiceObjective"], Is.AnyOf("NotApplicable", "Basic", "Standard", "Premium", "GeneralPurpose", "BusinessCritical", "Hyperscale", "ElasticPool"));
                Assert.That(double.Parse(data["Storage.SizeGB"], CultureInfo.InvariantCulture), Is.GreaterThanOrEqualTo(0));
                Assert.That(data["Storage.MessageCount"], Is.AnyOf("0", "Unknown"));
                Assert.That(data, Does.ContainKey("Health.FailedImports").WithValue("0"));
                Assert.That(data, Does.ContainKey("Storage.FullTextSearch").WithValue("Enabled"));
            }
        }

        [Test]
        public async Task Identifies_the_database_and_schema_it_writes_to()
        {
            var identity = await ServiceProvider.GetRequiredService<IStorageIdentityProvider>().GetIdentity(TestTimeoutCancellationToken);

            Assert.That(identity, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(identity.Engine, Is.EqualTo(PersisterName));
                Assert.That(identity.Server, Is.Not.Empty);
                Assert.That(identity.Database, Is.Not.Empty);
                Assert.That(identity.Schema, Is.EqualTo(configuration.Schema));
            }
        }
    }
}
