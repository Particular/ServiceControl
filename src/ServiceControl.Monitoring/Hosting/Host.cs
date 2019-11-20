namespace ServiceControl.Monitoring
{
    using System;
    using System.ComponentModel;
    using System.ServiceProcess;
    using NServiceBus;

    [DesignerCategory("Code")]
    class Host : ServiceBase
    {
        public Settings Settings { get; set; }

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

        void RunInteractive()
        {
            OnStart(null);
        }

        void RunAsService()
        {
            Run(this);
        }

        protected override void OnStart(string[] args)
        {
            var configuration = new EndpointConfiguration(Settings.ServiceName);

            bootstrapper = new Bootstrapper(
                    c => Environment.FailFast("NServiceBus Critical Error", c.Exception), 
                    Settings,
                    configuration);

            bootstrapper.Start().GetAwaiter().GetResult();
        }

        protected override void OnStop()
        {
            bootstrapper?.Stop().GetAwaiter().GetResult();

            OnStopping();
        }

        protected override void Dispose(bool disposing)
        {
            OnStop();
        }

        internal Action OnStopping = () => { };
        Bootstrapper bootstrapper;
    }
}