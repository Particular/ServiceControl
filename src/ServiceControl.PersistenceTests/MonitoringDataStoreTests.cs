namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Monitoring;

    [TestFixtureSource(typeof(PersistenceTestCollection))]
    class MonitoringDataStoreTests
    {
        PersistenceDataStoreFixture persistenceDataStoreFixture;

        public MonitoringDataStoreTests(PersistenceDataStoreFixture persistenceDataStoreFixture)
        {
            this.persistenceDataStoreFixture = persistenceDataStoreFixture;
        }

        [SetUp]
        public async Task Setup()
        {
            await persistenceDataStoreFixture.SetupDataStore().ConfigureAwait(false);
        }

        [TearDown]
        public async Task Cleanup()
        {
            await persistenceDataStoreFixture.CleanupDB().ConfigureAwait(false);
        }

        [Test]
        public async Task Endpoints_load_from_dataStore_into_monitor()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await persistenceDataStoreFixture.MonitoringDataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);

            await persistenceDataStoreFixture.CompleteDBOperation().ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);

            Assert.AreEqual(1, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host));
        }

        [Test]
        public async Task Endpoints_added_more_than_once_are_treated_as_same_endpoint()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await persistenceDataStoreFixture.MonitoringDataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);

            await persistenceDataStoreFixture.CompleteDBOperation().ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);

            Assert.AreEqual(1, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host));
        }

        [Test]
        public async Task Updating_existing_endpoint_does_not_create_new_ones()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await persistenceDataStoreFixture.MonitoringDataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.CreateOrUpdate(endpoint1, endpointInstanceMonitoring).ConfigureAwait(false);

            await persistenceDataStoreFixture.CompleteDBOperation().ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);

            Assert.AreEqual(1, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host));
        }

        [Test]
        public async Task Endpoint_is_created_if_doesnt_exist()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            var endpoint2 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host2", Name = "Name2" };
            await persistenceDataStoreFixture.MonitoringDataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.CreateIfNotExists(endpoint2).ConfigureAwait(false);

            await persistenceDataStoreFixture.CompleteDBOperation().ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);

            Assert.AreEqual(2, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host || w.HostDisplayName == endpoint2.Host));
        }

        [Test]
        public async Task Endpoint_is_created_if_doesnt_exist_on_update()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            var endpoint2 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host2", Name = "Name2" };
            await persistenceDataStoreFixture.MonitoringDataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.CreateOrUpdate(endpoint2, endpointInstanceMonitoring).ConfigureAwait(false);

            await persistenceDataStoreFixture.CompleteDBOperation().ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);

            Assert.AreEqual(2, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host || w.HostDisplayName == endpoint2.Host));
        }

        [Test]
        public async Task Endpoint_is_updated()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await persistenceDataStoreFixture.MonitoringDataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);

            await persistenceDataStoreFixture.CompleteDBOperation().ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);
            Assert.IsFalse(endpointInstanceMonitoring.IsMonitored(endpointInstanceMonitoring.GetEndpoints()[0].Id));

            await persistenceDataStoreFixture.MonitoringDataStore.UpdateEndpointMonitoring(endpoint1, true).ConfigureAwait(false);
            endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());

            await persistenceDataStoreFixture.CompleteDBOperation().ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);
            Assert.IsTrue(endpointInstanceMonitoring.IsMonitored(endpointInstanceMonitoring.GetEndpoints()[0].Id));
        }


        [Test]
        public async Task Endpoint_is_deleted()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await persistenceDataStoreFixture.MonitoringDataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);

            await persistenceDataStoreFixture.CompleteDBOperation().ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);
            Assert.AreEqual(1, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host));

            await persistenceDataStoreFixture.MonitoringDataStore.Delete(endpointInstanceMonitoring.GetEndpoints()[0].Id).ConfigureAwait(false);

            endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());

            await persistenceDataStoreFixture.CompleteDBOperation().ConfigureAwait(false);
            await persistenceDataStoreFixture.MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);
            Assert.AreEqual(0, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host));
        }
    }
}