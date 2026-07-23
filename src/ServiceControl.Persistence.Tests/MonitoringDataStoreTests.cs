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

            await CompleteDatabaseOperation();
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

            await CompleteDatabaseOperation();
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

            await CompleteDatabaseOperation();
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

            await CompleteDatabaseOperation();
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

            await CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);

            Assert.That(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host || w.HostDisplayName == endpoint2.Host), Is.EqualTo(2));
        }

        [Test]
        public async Task Endpoint_is_updated()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);

            await CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);
            Assert.That(endpointInstanceMonitoring.IsMonitored(endpointInstanceMonitoring.GetEndpoints()[0].Id), Is.False);

            await MonitoringDataStore.UpdateEndpointMonitoring(endpoint1, true);
            endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);

            await CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);
            Assert.That(endpointInstanceMonitoring.IsMonitored(endpointInstanceMonitoring.GetEndpoints()[0].Id), Is.True);
        }


        [Test]
        public async Task Endpoint_is_deleted()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);

            await CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);
            Assert.That(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host), Is.EqualTo(1));

            await MonitoringDataStore.Delete(endpointInstanceMonitoring.GetEndpoints()[0].Id);

            endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);

            await CompleteDatabaseOperation();
            await MonitoringDataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring);
            Assert.That(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host), Is.EqualTo(0));
        }

        [Test]
        public async Task GetAllKnownEndpoints_returns_created_endpoints()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            var endpoint2 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host2", Name = "Name2" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);
            await MonitoringDataStore.CreateOrUpdate(endpoint2, endpointInstanceMonitoring);

            await CompleteDatabaseOperation();
            var knownEndpoints = await MonitoringDataStore.GetAllKnownEndpoints();

            var known1 = knownEndpoints.Single(e => e.EndpointDetails.HostId == endpoint1.HostId);
            var known2 = knownEndpoints.Single(e => e.EndpointDetails.HostId == endpoint2.HostId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(known1.EndpointDetails.Name, Is.EqualTo(endpoint1.Name));
                Assert.That(known1.EndpointDetails.Host, Is.EqualTo(endpoint1.Host));
                Assert.That(known1.HostDisplayName, Is.EqualTo(endpoint1.Host));
                Assert.That(known1.Monitored, Is.False, "Endpoints created via CreateIfNotExists should not be monitored");
                Assert.That(known2.Monitored, Is.True, "Endpoints created via CreateOrUpdate should be monitored");
            }
        }

        [Test]
        public async Task Delete_removes_only_target_endpoint()
        {
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
            var endpoint2 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host2", Name = "Name2" };
            await MonitoringDataStore.CreateIfNotExists(endpoint1);
            await MonitoringDataStore.CreateIfNotExists(endpoint2);

            await MonitoringDataStore.Delete(endpoint1.GetDeterministicId());

            await CompleteDatabaseOperation();
            var knownEndpoints = await MonitoringDataStore.GetAllKnownEndpoints();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(knownEndpoints.Any(e => e.EndpointDetails.HostId == endpoint1.HostId), Is.False);
                Assert.That(knownEndpoints.Any(e => e.EndpointDetails.HostId == endpoint2.HostId), Is.True);
            }
        }

        [Test]
        public async Task Concurrent_creates_result_in_single_endpoint()
        {
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };

            await Task.WhenAll(Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() => MonitoringDataStore.CreateIfNotExists(endpoint1))));

            await CompleteDatabaseOperation();
            var knownEndpoints = await MonitoringDataStore.GetAllKnownEndpoints();

            Assert.That(knownEndpoints.Count(e => e.EndpointDetails.HostId == endpoint1.HostId), Is.EqualTo(1));
        }

        [Test]
        public async Task Concurrent_create_or_updates_result_in_single_endpoint()
        {
            var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);
            var endpoint1 = new EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };

            await Task.WhenAll(Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() => MonitoringDataStore.CreateOrUpdate(endpoint1, endpointInstanceMonitoring))));

            await CompleteDatabaseOperation();
            var knownEndpoints = await MonitoringDataStore.GetAllKnownEndpoints();

            Assert.That(knownEndpoints.Count(e => e.EndpointDetails.HostId == endpoint1.HostId), Is.EqualTo(1));
        }

        [Test]
        public async Task Unit_of_work_detects_endpoint()
        {
            var knownEndpoint = new KnownEndpoint
            {
                HostDisplayName = "Host1",
                EndpointDetails = new EndpointDetails { Host = "Host1", HostId = Guid.NewGuid(), Name = "Endpoint" }
            };

            await using (var unitOfWork = await UnitOfWorkFactory.StartNew())
            {
                await unitOfWork.Monitoring.RecordKnownEndpoint(knownEndpoint);

                await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
            }

            await CompleteDatabaseOperation();

            var knownEndpoints = await MonitoringDataStore.GetAllKnownEndpoints();

            Assert.That(knownEndpoints, Has.Count.EqualTo(1));
            var fromStorage = knownEndpoints[0];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(fromStorage.EndpointDetails.Host, Is.EqualTo(knownEndpoint.EndpointDetails.Host), "EndpointDetails.Host should match");
                Assert.That(fromStorage.EndpointDetails.HostId, Is.EqualTo(knownEndpoint.EndpointDetails.HostId), "EndpointDetails.HostId should match");
                Assert.That(fromStorage.EndpointDetails.Name, Is.EqualTo(knownEndpoint.EndpointDetails.Name), "EndpointDetails.Name should match");
                Assert.That(fromStorage.Monitored, Is.EqualTo(knownEndpoint.Monitored), "Monitored should match");
            }
        }

        [Test]
        public async Task Ingesting_a_known_endpoint_does_not_reset_monitored_flag()
        {
            var endpointDetails = new EndpointDetails { Host = "Host1", HostId = Guid.NewGuid(), Name = "Endpoint" };

            await MonitoringDataStore.CreateIfNotExists(endpointDetails);
            await MonitoringDataStore.UpdateEndpointMonitoring(endpointDetails, true);

            await CompleteDatabaseOperation();

            // Error ingestion always records endpoints it observes with Monitored = false
            // (ErrorProcessor.RecordKnownEndpoints), regardless of the endpoint's current
            // monitored state. Recording it again must not stomp a user-set Monitored = true.
            var observedAgain = new KnownEndpoint
            {
                HostDisplayName = endpointDetails.Host,
                EndpointDetails = endpointDetails,
                Monitored = false
            };

            await using (var unitOfWork = await UnitOfWorkFactory.StartNew())
            {
                await unitOfWork.Monitoring.RecordKnownEndpoint(observedAgain);

                await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
            }

            await CompleteDatabaseOperation();

            var knownEndpoints = await MonitoringDataStore.GetAllKnownEndpoints();
            var fromStorage = knownEndpoints.Single(e => e.EndpointDetails.HostId == endpointDetails.HostId);

            Assert.That(fromStorage.Monitored, Is.True, "Ingestion must not reset an existing endpoint's Monitored flag back to false");
        }

        [Test]
        public async Task Ingesting_a_previously_unknown_endpoint_creates_it_unmonitored()
        {
            var endpointDetails = new EndpointDetails { Host = "Host1", HostId = Guid.NewGuid(), Name = "Endpoint" };

            var observed = new KnownEndpoint
            {
                HostDisplayName = endpointDetails.Host,
                EndpointDetails = endpointDetails,
                Monitored = false
            };

            await using (var unitOfWork = await UnitOfWorkFactory.StartNew())
            {
                await unitOfWork.Monitoring.RecordKnownEndpoint(observed);

                await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
            }

            await CompleteDatabaseOperation();

            var knownEndpoints = await MonitoringDataStore.GetAllKnownEndpoints();
            var fromStorage = knownEndpoints.Single(e => e.EndpointDetails.HostId == endpointDetails.HostId);

            Assert.That(fromStorage.Monitored, Is.False, "A newly discovered endpoint should be created unmonitored");
        }
    }
}