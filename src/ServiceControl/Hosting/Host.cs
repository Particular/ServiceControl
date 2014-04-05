namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.ServiceProcess;

    internal class Host : ServiceBase
    {
        public void Run()
        {
            if (Environment.UserInteractive)
            {
                OnStart(null);
                return;
            }

            Run(this);
        }

        protected override void OnStart(string[] args)
        {
            bootstrapper = new Bootstrapper();
            bootstrapper.Start();
        }

        protected override void OnStop()
        {
            bootstrapper.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            OnStop();
        }

        Bootstrapper bootstrapper;
    }
}