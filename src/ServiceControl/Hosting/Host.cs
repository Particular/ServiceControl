namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.ServiceProcess;

    public class Host : ServiceBase
    {
        public void Run(bool interactive)
        {
            if (interactive)
            {
                RunInteractive();
            }
            else
            {
                RunAsService();
            }
        }

        private void RunInteractive()
        {
            OnStart(null);
        }

        private void RunAsService()
        {
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
            bootstrapper?.Stop();

            OnStopping();
        }

        protected override void Dispose(bool disposing)
        {
            OnStop();
        }

        Bootstrapper bootstrapper;
    }
}