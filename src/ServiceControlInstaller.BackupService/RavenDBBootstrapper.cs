
namespace ServiceControlInstaller.BackupStubService
{
    using Raven.Client.Embedded;
    using System;
    
    class RavenDbBootstrapper : IDisposable
    {
        EmbeddableDocumentStore store;
        
        /// **************************
        /// DO NOT UPGRADE TO RAVEN 3
        /// 
        /// The point of this exe is that it is a minimal drop in replacement for older versions of ServiceControl.exe
        /// This exe will expose the RavenDB HTTP stack so we can initiate a backup prior to upgrading
        /// 
        /// **************************

        public RavenDbBootstrapper()
        {
            var settings = new Settings();
            store = new EmbeddableDocumentStore
            {
                DataDirectory = settings.DBPath,
                UseEmbeddedHttpServer = true,
                EnlistInDistributedTransactions = false
            };
            store.Configuration.DisableClusterDiscovery = true;
            store.Configuration.DisablePerformanceCounters = true;
            store.Configuration.CompiledIndexCacheDirectory = settings.DBPath;
            store.Configuration.Port = settings.Port;
            store.Configuration.HostName =  "localhost";
            store.Configuration.VirtualDirectory = settings.VirtualDirectory + "/storage";
            store.Conventions.SaveEnumsAsIntegers = true;
            store.Initialize();
            Console.WriteLine($"RavenDB is running on {store.Configuration.ServerUrl}");

        }
        
        public void Dispose()
        {
          store?.Dispose();
        }
    }
}
