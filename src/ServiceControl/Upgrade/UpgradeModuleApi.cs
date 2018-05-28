namespace Particular.ServiceControl.Upgrade
{
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class UpgradeModuleApi : BaseModule
    {
        public StaleIndexInfoStore InfoStore { get; set; }
        
        public UpgradeModuleApi()
        {
            Get["/upgrade"] = parameters =>
            {
                var info = InfoStore.Get();
                return Negotiate.WithModel(info);
            };
        }
    }
}