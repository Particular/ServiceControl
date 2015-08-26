namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System.Globalization;
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using Microsoft.AspNet.SignalR.Json;
    using Microsoft.Owin.Hosting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.SignalR;

    class ApiFeature : Feature
    {
        public ApiFeature()
        {
            EnableByDefault();
            RegisterStartupTask<ConfigureSignalR>();
            RegisterStartupTask<BootstrapApi>();
        }

        protected override void Setup(FeatureConfigurationContext context) { }

        class ConfigureSignalR : FeatureStartupTask
        {
            protected override void OnStart()
            {
                //var serializer = new JsonNetSerializer(new JsonSerializerSettings
                //{
                //    ContractResolver = new CustomSignalRContractResolverBecauseOfIssue500InSignalR(),
                //    Formatting = Formatting.None,
                //    NullValueHandling = NullValueHandling.Ignore,
                //    Converters = { 
                //    new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.RoundtripKind }, 
                //    new StringEnumConverter { CamelCaseText = true }
                //}
                //});

                //GlobalHost.DependencyResolver.Register(typeof(IJsonSerializer), () => serializer);
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