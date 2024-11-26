namespace ServiceControl.Persistence.Tests.RavenDB.ConnectedApplications
{
    using System.Linq;
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

            var connectedApplication1 = new ConnectedApplication { Name = "ServiceControl.Connector.MassTransit" };
            var connectedApplication2 = new ConnectedApplication { Name = "ServiceControl.Connector.Kafka" };

            await connectedApplicationsDataStore.UpdateConnectedApplication(connectedApplication1, CancellationToken.None).ConfigureAwait(false);
            await connectedApplicationsDataStore.UpdateConnectedApplication(connectedApplication2, CancellationToken.None).ConfigureAwait(false);

            var result = await connectedApplicationsDataStore.GetAllConnectedApplications();

            Assert.That(result.Select(x => x.Name), Is.EqualTo(new[] { connectedApplication1.Name, connectedApplication2.Name }).AsCollection);
        }
    }
}