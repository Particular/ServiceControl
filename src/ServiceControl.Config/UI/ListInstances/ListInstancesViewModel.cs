namespace ServiceControl.Config.UI.ListInstances
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using Caliburn.Micro;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControlInstaller.Engine.Instances;
    using System.Threading.Tasks;

    class ListInstancesViewModel : RxScreen, IHandle<RefreshInstances>, IHandle<LicenseUpdated>
    {
        private readonly Func<ServiceControlInstance, InstanceDetailsViewModel> instanceDetailsFunc;

        public ListInstancesViewModel(Func<ServiceControlInstance, InstanceDetailsViewModel> instanceDetailsFunc)
        {
            this.instanceDetailsFunc = instanceDetailsFunc;
            DisplayName = "DEPLOYED INSTANCES";

            Instances = new BindableCollection<InstanceDetailsViewModel>();

            RefreshInstances();
        }

        public IList<InstanceDetailsViewModel> Instances { get; }

        public void Handle(RefreshInstances message)
        {
            RefreshInstances();
        }

        private void RefreshInstances()
        {
            var currentInstances = InstanceFinder.ServiceControlInstances();

            var addedInstances = currentInstances.Where(i => Instances.All(i2 => i2.Name != i.Name)).ToList();
            var removedInstances = Instances.Where(i => currentInstances.All(i2 => i2.Name != i.Name)).ToList();

            foreach (var item in addedInstances)
            {
                Instances.Add(instanceDetailsFunc(item));
            }

            foreach (var item in removedInstances)
            {
                Instances.Remove(item);
            }

            foreach (var instance in Instances)
            {
                instance.ServiceControlInstance.Reload();
            }
            // Existing instances are updated in the InstanceDetailsViewModel
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