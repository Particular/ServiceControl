namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System.Threading.Tasks;
    using Microsoft.Owin.Hosting;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    class ApiFeature : Feature
    {
        public ApiFeature()
        {
            EnableByDefault();
            RegisterStartupTask<BootstrapApi>();
        }

        protected override void Setup(FeatureConfigurationContext context) { }

        class BootstrapApi : FeatureStartupTask
        {
            protected override void OnStart()
            {
                task = Task.Factory.StartNew(StartWebApp);
            }

            protected override void OnStop()
            {
                if (task != null && !task.IsCompleted)
                {
                    task.Wait();
                }
            }

            void StartWebApp()
            {
                WebApp.Start<Startup>(Settings.ApiUrl);
                Logger.InfoFormat("Api is now accepting requests on {0}", Settings.ApiUrl);                
            }

            Task task;
            static readonly ILog Logger = LogManager.GetLogger(typeof(ApiFeature));
        }
    }
}