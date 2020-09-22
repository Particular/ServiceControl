namespace ServiceControl.Audit.Infrastructure.Hosting
{
    using System.ServiceProcess;
    using Raven.Client.Documents;
    using Settings;

    class MaintenanceHost : ServiceBase
    {
        // TODO: RAVEN5 - Changed from EmbeddableDocumentStore. Figure out where this is used and if it is still needed.
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