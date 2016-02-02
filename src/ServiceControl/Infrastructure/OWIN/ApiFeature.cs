namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using Microsoft.AspNet.SignalR.Json;
    using Microsoft.Owin.Hosting;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.SignalR;

    class ApiFeature : Feature
    {
        public ApiFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(builder => builder.Build<ConfigureSignalR>());
            context.RegisterStartupTask(builder => builder.Build<BootstrapApi>());
        }

        class ConfigureSignalR : FeatureStartupTask
        {
            protected override void OnStart()
            {
                GlobalHost.DependencyResolver.Register(typeof(IJsonSerializer), SerializationSettingsFactoryForSignalR.CreateDefault);
            }
        }

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