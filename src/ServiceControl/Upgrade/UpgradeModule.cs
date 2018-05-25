namespace Particular.ServiceControl.Upgrade
{
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class UpgradeModule : BaseModule
    {
        public StaleIndexInfoStore InfoStore { get; set; }
        
        public UpgradeModule()
        {
            Get["/upgrade"] = parameters =>
            {
                var info = InfoStore.Get();
                return Negotiate.WithModel(info);
            };
        }
    }
}