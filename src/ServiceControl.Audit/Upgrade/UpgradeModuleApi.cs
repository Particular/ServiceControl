﻿namespace Particular.ServiceControl.Upgrade
{
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    class UpgradeModuleApi : BaseModule
    {
        public UpgradeModuleApi()
        {
            Get["/upgrade"] = parameters =>
            {
                var info = InfoStore?.Get();
                return Negotiate.WithModel(info);
            };
        }

        public StaleIndexInfoStore InfoStore { get; set; }
    }
}