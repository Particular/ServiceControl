namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Monitoring;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    class MonitoringDataStoreTests : PersistenceTestBase
    {
        [Test]
        public async Task Endpoints_load_from_dataStore_into_monitor()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);

            Assert.AreEqual(1, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host));
        }

        [Test]
        public async Task Endpoints_added_more_than_once_are_treated_as_same_endpoint()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);
            await MonitoringDataStore.CreateIfNotExists(endpoint1);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);

            Assert.AreEqual(1, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host));
        }

        [Test]
        public async Task Updating_existing_endpoint_does_not_create_new_ones()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);
            await MonitoringDataStore.CreateOrUpdate(endpoint1, endpointInstanceMonitoring);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);

            Assert.AreEqual(1, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host));
        }

        [Test]
        public async Task Endpoint_is_created_if_doesnt_exist()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            var endpoint2 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host2", Name = "Name2" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);
            await MonitoringDataStore.CreateIfNotExists(endpoint2);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);

            Assert.AreEqual(2, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host || w.HostDisplayName == endpoint2.Host));
        }

        [Test]
        public async Task Endpoint_is_created_if_doesnt_exist_on_update()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            var endpoint2 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host2", Name = "Name2" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);
            await MonitoringDataStore.CreateOrUpdate(endpoint2, endpointInstanceMonitoring);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);

            Assert.AreEqual(2, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host || w.HostDisplayName == endpoint2.Host));
        }

        [Test]
        public async Task Endpoint_is_updated()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);
            Assert.IsFalse(endpointInstanceMonitoring.IsMonitored(endpointInstanceMonitoring.GetEndpoints()[0].Id));

            await MonitoringDataStore.UpdateEndpointMonitoring(endpoint1, true);
            endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);
            Assert.IsTrue(endpointInstanceMonitoring.IsMonitored(endpointInstanceMonitoring.GetEndpoints()[0].Id));
        }


        [Test]
        public async Task Endpoint_is_deleted()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);
            Assert.AreEqual(1, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host));

            await MonitoringDataStore.Delete(endpointInstanceMonitoring.GetEndpoints()[0].Id);

            endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);
            Assert.AreEqual(0, endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host));
        }

        [Test]
        public async Task Unit_of_work_detects_endpoint()
        {
            var knownEndpoint = new KnownEndpoint
            {
                HostDisplayName = "Host1",
                EndpointDetails = new EndpointDetails { Host = "Host1", HostId = Guid.NewGuid(), Name = "Endpoint" }
            };

            using (var unitOfWork = await UnitOfWorkFactory.StartNew())
            {
                await unitOfWork.Monitoring.RecordKnownEndpoint(knownEndpoint);

                await unitOfWork.Complete();
            }

            CompleteDatabaseOperation();

            var knownEndpoints = await MonitoringDataStore.GetAllKnownEndpoints();

            Assert.AreEqual(1, knownEndpoints.Count);
            var fromStorage = knownEndpoints[0];

            Assert.AreEqual(knownEndpoint.EndpointDetails.Host, fromStorage.EndpointDetails.Host, "EndpointDetails.Host should match");
            Assert.AreEqual(knownEndpoint.EndpointDetails.HostId, fromStorage.EndpointDetails.HostId, "EndpointDetails.HostId should match");
            Assert.AreEqual(knownEndpoint.EndpointDetails.Name, fromStorage.EndpointDetails.Name, "EndpointDetails.Name should match");
            Assert.AreEqual(knownEndpoint.Monitored, fromStorage.Monitored, "Monitored should match");
        }
    }
}