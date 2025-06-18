namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;
    using ServiceControl.Monitoring;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    class MonitoringDataStoreTests : PersistenceTestBase
    {
        [Test]
        public async Task Endpoints_load_from_dataStore_into_monitor()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);

            Assert.That(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host), Is.EqualTo(1));
        }

        [Test]
        public async Task Endpoints_added_more_than_once_are_treated_as_same_endpoint()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);
            await MonitoringDataStore.CreateIfNotExists(endpoint1);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);

            Assert.That(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host), Is.EqualTo(1));
        }

        [Test]
        public async Task Updating_existing_endpoint_does_not_create_new_ones()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);
            await MonitoringDataStore.CreateOrUpdate(endpoint1, endpointInstanceMonitoring);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);

            Assert.That(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host), Is.EqualTo(1));
        }

        [Test]
        public async Task Endpoint_is_created_if_doesnt_exist()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            var endpoint2 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host2", Name = "Name2" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);
            await MonitoringDataStore.CreateIfNotExists(endpoint2);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);

            Assert.That(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host || w.HostDisplayName == endpoint2.Host), Is.EqualTo(2));
        }

        [Test]
        public async Task Endpoint_is_created_if_doesnt_exist_on_update()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            var endpoint2 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host2", Name = "Name2" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);
            await MonitoringDataStore.CreateOrUpdate(endpoint2, endpointInstanceMonitoring);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);

            Assert.That(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host || w.HostDisplayName == endpoint2.Host), Is.EqualTo(2));
        }

        [Test]
        public async Task Endpoint_is_updated()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);
            Assert.That(endpointInstanceMonitoring.IsMonitored(endpointInstanceMonitoring.GetEndpoints()[0].Id), Is.False);

            await MonitoringDataStore.UpdateEndpointMonitoring(endpoint1, true);
            endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);
            Assert.That(endpointInstanceMonitoring.IsMonitored(endpointInstanceMonitoring.GetEndpoints()[0].Id), Is.True);
        }


        [Test]
        public async Task Endpoint_is_deleted()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);
            Assert.That(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host), Is.EqualTo(1));

            await MonitoringDataStore.Delete(endpointInstanceMonitoring.GetEndpoints()[0].Id);

            endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);

            CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);
            Assert.That(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host), Is.EqualTo(0));
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

                await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
            }

            CompleteDatabaseOperation();

            var knownEndpoints = await MonitoringDataStore.GetAllKnownEndpoints();

            Assert.That(knownEndpoints, Has.Count.EqualTo(1));
            var fromStorage = knownEndpoints[0];

            Assert.Multiple(() =>
            {
                Assert.That(fromStorage.EndpointDetails.Host, Is.EqualTo(knownEndpoint.EndpointDetails.Host), "EndpointDetails.Host should match");
                Assert.That(fromStorage.EndpointDetails.HostId, Is.EqualTo(knownEndpoint.EndpointDetails.HostId), "EndpointDetails.HostId should match");
                Assert.That(fromStorage.EndpointDetails.Name, Is.EqualTo(knownEndpoint.EndpointDetails.Name), "EndpointDetails.Name should match");
                Assert.That(fromStorage.Monitored, Is.EqualTo(knownEndpoint.Monitored), "Monitored should match");
            });
        }
    }
}