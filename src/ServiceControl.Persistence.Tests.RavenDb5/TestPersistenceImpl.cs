namespace ServiceControl.PersistenceTests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using Persistence;
    using Raven.Client.Documents;
    using ServiceControl.Persistence.RavenDb;

    sealed class TestPersistenceImpl : TestPersistence
    {
        readonly RavenDBPersisterSettings settings = CreateSettings();
        IDocumentStore documentStore;

        static RavenDBPersisterSettings CreateSettings()
        {
            var retentionPeriod = TimeSpan.FromMinutes(1);

            var settings = new RavenDBPersisterSettings
            {
                AuditRetentionPeriod = retentionPeriod,
                ErrorRetentionPeriod = retentionPeriod,
                EventsRetentionPeriod = retentionPeriod,
                RunInMemory = true,
                DatabasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "ErrorData"),
                LogPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Logs"),
                LogsMode = "Operations",
                ServerUrl = $"http://localhost:{FindAvailablePort(33334)}"
            };

            if (Debugger.IsAttached)
            {
                Console.WriteLine("If you get 'Access is denied' exception while debugging, comment out this line or create a URLACL reservervation:");
                Console.WriteLine("> netsh http add urlacl http://+:55554/ user=Everyone");
                settings.ExposeRavenDB = true;
            }

            return settings;
        }

        public override void Configure(IServiceCollection services)
        {
            var persistence = new RavenDbPersistenceConfiguration().Create(CreateSettings());

            PersistenceHostBuilderExtensions.CreatePersisterLifecyle(services, persistence);

            services.AddHostedService(p => new Wrapper(this, p.GetRequiredService<IDocumentStore>()));
        }

        public override Task CompleteDatabaseOperation()
        {
            Assert.IsNotNull(documentStore);
            documentStore.WaitForIndexing();
            return Task.CompletedTask;
        }

        //TODO: this method is duplicated at least 3 times in the test project
        static int FindAvailablePort(int startPort)
        {
            var activeTcpListeners = IPGlobalProperties
                .GetIPGlobalProperties()
                .GetActiveTcpListeners();

            for (var port = startPort; port < startPort + 1024; port++)
            {
                var portCopy = port;
                if (activeTcpListeners.All(endPoint => endPoint.Port != portCopy))
                {
                    return port;
                }
            }

            return startPort;
        }
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

            var url = $"http://localhost:{settings.DatabaseMaintenancePort}/studio/index.html#databases/documents?&database=%3Csystem%3E";

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

            while (true)
            {
                Thread.Sleep(5000);
                Trace.Write("Waiting for debugger pause");
            }
        }
    }
}