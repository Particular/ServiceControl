namespace ServiceControl.Config.UI.ListInstances
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Caliburn.Micro;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControlInstaller.Engine.Instances;
    using System.Threading.Tasks;

    class ListInstancesViewModel : RxScreen, IHandle<RefreshInstances>, IHandle<LicenseUpdated>
    {
        private readonly Func<BaseService, InstanceDetailsViewModel> instanceDetailsFunc;

        public ListInstancesViewModel(Func<BaseService, InstanceDetailsViewModel> instanceDetailsFunc)
        {
            this.instanceDetailsFunc = instanceDetailsFunc;
            DisplayName = "";

            Instances = new BindableCollection<InstanceDetailsViewModel>();

            RefreshInstances();
        }

        public bool ShowOnlyUpgrades { private get; set; }

        public IList<InstanceDetailsViewModel> Instances { get; }

        public void Handle(RefreshInstances message)
        {
            RefreshInstances();
        }

        private void RefreshInstances()
        {
            Instances.Clear();
            foreach (var item in InstanceFinder.AllInstances())
            {
                var instance = instanceDetailsFunc(item);
                if (ShowOnlyUpgrades && !instance.HasNewVersion) continue;
                Instances.Add(instance);
            }
        }

        public void Handle(LicenseUpdated licenseUpdatedEvent)
        {
            // on license change inform each instance to refresh the license (1.23.0 and below don't support this)
            foreach (var instance in Instances)
            {
                if (instance.Version <= new Version("1.23.0")) continue;
                Task.Run(() =>
                {
                    try
                    {
                        var request = WebRequest.Create($"{instance.BrowsableUrl}license?refresh=true");
                        request.Timeout = 2000;
                        request.GetResponse();
                    }
                    catch
                    {
                        // Ignored
                    }
                });
            }
        }
    }
}