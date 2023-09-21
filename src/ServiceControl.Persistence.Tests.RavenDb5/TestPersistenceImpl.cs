namespace ServiceControl.PersistenceTests
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using Persistence;
    using Raven.Client.Documents;
    using Raven.Client.ServerWide.Operations;
    using ServiceControl.Persistence.RavenDb;

    sealed class TestPersistenceImpl : TestPersistence
    {
        readonly string databaseName;

        readonly RavenDBPersisterSettings settings;
        IDocumentStore documentStore;

        public TestPersistenceImpl()
        {
            databaseName = Guid.NewGuid().ToString("n");
            var retentionPeriod = TimeSpan.FromMinutes(1);

            TestContext.Out.WriteLine($"Test Database Name: {databaseName}");

            settings = new RavenDBPersisterSettings
            {
                AuditRetentionPeriod = retentionPeriod,
                ErrorRetentionPeriod = retentionPeriod,
                EventsRetentionPeriod = retentionPeriod,
                ConnectionString = SetUpFixture.SharedInstance.ServerUrl,
                DatabaseName = databaseName
            };
        }

        public override void Configure(IServiceCollection services)
        {
            var persistence = new RavenDbPersistenceConfiguration().Create(settings);
            PersistenceHostBuilderExtensions.CreatePersisterLifecyle(services, persistence);
            services.AddHostedService(p => new Wrapper(this, p.GetRequiredService<IDocumentStore>()));
        }

        public override void CompleteDatabaseOperation()
        {
            Assert.IsNotNull(documentStore);
            documentStore.WaitForIndexing();
        }

        // TODO: Doesn't appear to be doing anything in the Start/Stop, do we need this?
        class Wrapper : IHostedService
        {
            public Wrapper(TestPersistenceImpl instance, IDocumentStore store)
            {
                instance.documentStore = store;
            }

            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        public override void BlockToInspectDatabase()
        {
            if (!Debugger.IsAttached)
            {
                return;
            }

            var url = SetUpFixture.SharedInstance.ServerUrl + "/studio/index.html#databases/documents?&database=" + databaseName;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }

            Debugger.Break();
        }

        public override async Task TearDown()
        {
            // Comment this out temporarily to be able to inspect a database after the test has completed
            var deleteDatabasesOperation = new DeleteDatabasesOperation(new DeleteDatabasesOperation.Parameters { DatabaseNames = new[] { databaseName }, HardDelete = true });
            await documentStore.Maintenance.Server.SendAsync(deleteDatabasesOperation);
        }
    }
}