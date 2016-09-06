namespace ServiceControlInstaller.BackupStubService
{
    using System.ServiceProcess;
    public class StubService : ServiceBase
    {
        private RavenDbBootstrapper bootstrapper;

        internal void InteractiveStart()
        {
            OnStart(null);
        }

        internal void InteractiveStop()
        {
            OnStop();
        }

        protected override void OnStart(string[] args)
        {
            bootstrapper = new RavenDbBootstrapper();
        }

        protected override void OnStop()
        {
            bootstrapper?.Dispose();
        }
    }
}
