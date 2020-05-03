namespace ServiceControl.Monitoring
{
    using System;
    using System.ComponentModel;
    using System.ServiceProcess;
    using NServiceBus;

    [DesignerCategory("Code")]
    class Host : ServiceBase
    {
        public Host(bool logToConsole)
        {
            this.logToConsole = logToConsole;
        }

        public Settings Settings { get; set; }

        public void Run()
        {
            if (logToConsole)
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
                    c => { }, //Do nothing. The transports in NSB 7 are designed to handle broker outages 
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

        bool logToConsole;
        internal Action OnStopping = () => { };
        Bootstrapper bootstrapper;
    }
}