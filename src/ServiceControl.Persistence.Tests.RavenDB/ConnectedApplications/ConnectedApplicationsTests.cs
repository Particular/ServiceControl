namespace ServiceControl.Persistence.Tests.RavenDB.ConnectedApplications
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using ServiceControl.Persistence.RavenDB;

    [TestFixture]
    class ConnectedApplicationsTests : RavenPersistenceTestBase
    {
        public ConnectedApplicationsTests() =>
            RegisterServices = services =>
            {
                services.AddSingleton<ConnectedApplicationsDataStore>();
            };

        [Test]
        public async Task ConnectedApplications_can_be_saved()
        {
            var connectedApplicationsDataStore = ServiceProvider.GetRequiredService<ConnectedApplicationsDataStore>();

            var connectedApplication1 = "ServiceControl.Connector.MassTransit";
            var connectedApplication2 = "ServiceControl.Connector.Kafka";

            await connectedApplicationsDataStore.Add(connectedApplication1).ConfigureAwait(false);
            await connectedApplicationsDataStore.Add(connectedApplication2).ConfigureAwait(false);

            var result = await connectedApplicationsDataStore.GetConnectedApplications();

            Assert.That(result, Is.EqualTo(new[] { connectedApplication1, connectedApplication2 }).AsCollection);
        }
    }
}