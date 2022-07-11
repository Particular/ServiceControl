namespace ServiceControl.Persistence.Tests
{
    //WORK IN PROGRESS
    //using System;
    //using System.Collections.Generic;
    //using System.ComponentModel.Composition.Hosting;
    //using System.Data.SqlClient;
    //using System.Linq;
    //using System.Reflection;
    //using System.Threading.Tasks;
    //using Dapper;
    //using NUnit.Framework;
    //using Raven.Client.Embedded;
    //using Raven.Client.Indexes;
    //using ServiceControl.Infrastructure.RavenDB;
    //using ServiceControl.Monitoring;
    //using static Dapper.SqlMapper;

    //class MonitoringDataStoreTests
    //{
    //    public async Task SetupSqlDb()
    //    {
    //        using (var connection = new SqlConnection(TestFixtureSetup.SqlDbConnectionString))
    //        {
    //            var catalog = new SqlConnectionStringBuilder(TestFixtureSetup.SqlDbConnectionString).InitialCatalog;

    //            var setupCommand = $@"
    //                IF NOT EXISTS (
    //                     SELECT *
    //                     FROM {catalog}.sys.objects
    //                     WHERE object_id = OBJECT_ID(N'KnownEndpoints') AND type in (N'U')
    //                   )
    //                   BEGIN
    //                       CREATE TABLE [dbo].[KnownEndpoints](
    //                        [Id] [uniqueidentifier] NOT NULL,
    //                        [HostId] [uniqueidentifier] NOT NULL,
    //                        [Host] [nvarchar](300) NULL,
    //                        [HostDisplayName] [nvarchar](300) NULL,
    //                        [Monitored] [bit] NOT NULL
    //                       ) ON [PRIMARY]
    //                   END
    //                ELSE
    //                   BEGIN
    //                       Truncate TABLE [dbo].[KnownEndpoints]
    //                   END";

    //            connection.Open();

    //            await connection.ExecuteAsync(setupCommand).ConfigureAwait(false);
    //        }
    //    }

    //    ExportProvider CreateIndexProvider(List<Assembly> indexAssemblies) =>
    //       new CompositionContainer(
    //           new AggregateCatalog(
    //               from indexAssembly in indexAssemblies select new AssemblyCatalog(indexAssembly)
    //           )

    //       );
    //    static EmbeddableDocumentStore documentStore;

    //    public async Task SetupRaven()
    //    {
    //        var settings = new ServiceBus.Management.Infrastructure.Settings.Settings()
    //        {
    //            RunInMemory = true,
    //        };
    //        documentStore = new EmbeddableDocumentStore();
    //        RavenBootstrapper.Configure(documentStore, settings);
    //        documentStore.Initialize();


    //        var indexProvider = CreateIndexProvider(new List<Assembly>() { typeof(RavenBootstrapper).Assembly });
    //        await IndexCreation.CreateIndexesAsync(indexProvider, documentStore)
    //            .ConfigureAwait(false);
    //    }

    //    public Task CleanupRaven()
    //    {
    //        documentStore.Dispose();
    //        return Task.CompletedTask;
    //    }

    //    async Task SetupDBs(IMonitoringDataStore dataStore)
    //    {
    //        if (dataStore is SqlDbMonitoringDataStore)
    //        {
    //            await SetupSqlDb().ConfigureAwait(false);
    //        }
    //        else if (dataStore is RavenDbMonitoringDataStore)
    //        {
    //            await SetupRaven().ConfigureAwait(false);
    //        }
    //    }

    //    async Task CleanupDBs(IMonitoringDataStore dataStore)
    //    {
    //        if (dataStore is RavenDbMonitoringDataStore)
    //        {
    //            await CleanupRaven().ConfigureAwait(false);
    //        }
    //    }

    //    public static IEnumerable<TestCaseData> GetPersistenceTestCases
    //    {
    //        get
    //        {
    //            yield return new TestCaseData(new InMemoryMonitoringDataStore());
    //            yield return new TestCaseData(new SqlDbMonitoringDataStore(TestFixtureSetup.SqlDbConnectionString));
    //            //yield return new TestCaseData(new RavenDbMonitoringDataStore(documentStore));
    //        }
    //    }

    //    [Test]
    //    [TestCaseSource("GetPersistenceTestCases")]
    //    public async Task Endpoints_load_from_dataStore_into_monitor(IMonitoringDataStore dataStore)
    //    {
    //        await SetupDBs(dataStore).ConfigureAwait(false);

    //        var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
    //        var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
    //        await dataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);

    //        await dataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);

    //        Assert.AreEqual(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host), 1);
    //    }

    //    [Test]
    //    [TestCaseSource("GetPersistenceTestCases")]
    //    public async Task Endpoints_added_more_than_once_are_treated_as_same_endpoint(IMonitoringDataStore dataStore)
    //    {
    //        await SetupDBs(dataStore).ConfigureAwait(false);

    //        var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
    //        var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
    //        await dataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);
    //        await dataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);

    //        await dataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);

    //        Assert.AreEqual(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host), 1);

    //        await CleanupDBs(dataStore).ConfigureAwait(false);
    //    }

    //    [Test]
    //    [TestCaseSource("GetPersistenceTestCases")]
    //    public async Task Updating_existing_endpoint_does_not_create_new_ones(IMonitoringDataStore dataStore)
    //    {
    //        await SetupDBs(dataStore).ConfigureAwait(false);

    //        var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
    //        var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
    //        await dataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);
    //        await dataStore.CreateOrUpdate(endpoint1, endpointInstanceMonitoring).ConfigureAwait(false);

    //        await dataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);

    //        Assert.AreEqual(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host), 1);

    //        await CleanupDBs(dataStore).ConfigureAwait(false);
    //    }

    //    [Test]
    //    [TestCaseSource("GetPersistenceTestCases")]
    //    public async Task Endpoint_is_created_if_doesnt_exist(IMonitoringDataStore dataStore)
    //    {
    //        await SetupDBs(dataStore).ConfigureAwait(false);

    //        var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
    //        var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
    //        var endpoint2 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host2", Name = "Name2" };
    //        await dataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);
    //        await dataStore.CreateIfNotExists(endpoint2).ConfigureAwait(false);

    //        await dataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);

    //        Assert.AreEqual(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host || w.HostDisplayName == endpoint2.Host), 2);

    //        await CleanupDBs(dataStore).ConfigureAwait(false);
    //    }

    //    [Test]
    //    [TestCaseSource("GetPersistenceTestCases")]
    //    public async Task Endpoint_is_created_if_doesnt_exist_on_update(IMonitoringDataStore dataStore)
    //    {
    //        await SetupDBs(dataStore).ConfigureAwait(false);

    //        var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
    //        var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
    //        var endpoint2 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host2", Name = "Name2" };
    //        await dataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);
    //        await dataStore.CreateOrUpdate(endpoint2, endpointInstanceMonitoring).ConfigureAwait(false);

    //        await dataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);

    //        Assert.AreEqual(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host || w.HostDisplayName == endpoint2.Host), 2);

    //        await CleanupDBs(dataStore).ConfigureAwait(false);
    //    }

    //    [Test]
    //    [TestCaseSource("GetPersistenceTestCases")]
    //    public async Task Endpoint_is_updated(IMonitoringDataStore dataStore)
    //    {
    //        await SetupDBs(dataStore).ConfigureAwait(false);

    //        var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
    //        var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
    //        await dataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);
    //        await dataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);
    //        Assert.IsFalse(endpointInstanceMonitoring.IsMonitored(endpointInstanceMonitoring.GetEndpoints()[0].Id));

    //        await dataStore.UpdateEndpointMonitoring(endpoint1, true).ConfigureAwait(false);
    //        endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
    //        await dataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);
    //        Assert.IsTrue(endpointInstanceMonitoring.IsMonitored(endpointInstanceMonitoring.GetEndpoints()[0].Id));

    //        await CleanupDBs(dataStore).ConfigureAwait(false);
    //    }


    //    [Test]
    //    [TestCaseSource("GetPersistenceTestCases")]
    //    public async Task Endpoint_is_deleted(IMonitoringDataStore dataStore)
    //    {
    //        await SetupDBs(dataStore).ConfigureAwait(false);

    //        var endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
    //        var endpoint1 = new Contracts.Operations.EndpointDetails() { HostId = Guid.NewGuid(), Host = "Host1", Name = "Name1" };
    //        await dataStore.CreateIfNotExists(endpoint1).ConfigureAwait(false);
    //        await dataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);
    //        Assert.AreEqual(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host), 1);

    //        await dataStore.Delete(endpointInstanceMonitoring.GetEndpoints()[0].Id).ConfigureAwait(false);

    //        endpointInstanceMonitoring = new EndpointInstanceMonitoring(new FakeDomainEvents());
    //        await dataStore.WarmupMonitoringFromPersistence(endpointInstanceMonitoring).ConfigureAwait(false);
    //        Assert.AreEqual(endpointInstanceMonitoring.GetKnownEndpoints().Count(w => w.HostDisplayName == endpoint1.Host), 0);

    //        await CleanupDBs(dataStore).ConfigureAwait(false);
    //    }
    //}
}