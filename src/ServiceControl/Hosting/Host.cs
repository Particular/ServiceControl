namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.ServiceProcess;

    public class Host : ServiceBase
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
            bootstrapper = new Bootstrapper(this);
            bootstrapper.Start();
        }

        internal Action OnStopping = () => { };

        protected override void OnStop()
        {
            bootstrapper.Stop();

            OnStopping();
        }

        protected override void Dispose(bool disposing)
        {
            OnStop();
        }

        Bootstrapper bootstrapper;
    }
}