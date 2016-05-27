namespace ServiceControl.Config.UI.ListInstances
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Caliburn.Micro;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControlInstaller.Engine.Instances;

    class ListInstancesViewModel : RxScreen, IHandle<RefreshInstances>
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
            var currentInstances = ServiceControlInstance.Instances();

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
    }
}