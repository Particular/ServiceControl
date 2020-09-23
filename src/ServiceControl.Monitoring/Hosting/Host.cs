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

        public void Start()
        {
            OnStart(null);
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

        internal Action OnStopping = () => { };
        Bootstrapper bootstrapper;
    }
}