namespace Particular.ServiceControl.Hosting
{
    using System.ServiceProcess;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    public class MaintenanceHost : ServiceBase
    {
        public MaintenanceHost(Settings settings, EmbeddableDocumentStore documentStore)
        {
            this.documentStore = documentStore;
            ServiceName = settings.ServiceName;
        }

        public void Run()
        {
            Run(this);
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
            documentStore?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            OnStop();
        }

        readonly EmbeddableDocumentStore documentStore;
    }
}