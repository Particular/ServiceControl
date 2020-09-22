namespace Particular.ServiceControl.Hosting
{
    using System.ServiceProcess;
    using Raven.Client.Documents;
    using ServiceBus.Management.Infrastructure.Settings;

    class MaintenanceHost : ServiceBase
    {
        //TODO: RAVEN5 EmbeddableDocumentStore replacement, we should consider if this class is needed!
        public MaintenanceHost(Settings settings, IDocumentStore documentStore)
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

        readonly IDocumentStore documentStore;
    }
}