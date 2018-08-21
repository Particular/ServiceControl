namespace ServiceControl.Config.UI.ListInstances
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework.Rx;
    using InstanceDetails;
    using ServiceControlInstaller.Engine.Instances;

    class ListInstancesViewModel : RxScreen, IHandle<RefreshInstances>, IHandle<ResetInstances>, IHandle<LicenseUpdated>
    {
        public ListInstancesViewModel(Func<BaseService, InstanceDetailsViewModel> instanceDetailsFunc)
        {
            this.instanceDetailsFunc = instanceDetailsFunc;
            DisplayName = "DEPLOYED INSTANCES";

            Instances = new BindableCollection<InstanceDetailsViewModel>();

            RefreshInstances();
        }

        public IList<InstanceDetailsViewModel> Instances { get; }

        public void Handle(LicenseUpdated licenseUpdatedEvent)
        {
            // on license change inform each instance to refresh the license (1.23.0 and below don't support this)
            foreach (var instance in Instances)
            {
                if (instance.Version <= new Version("1.23.0"))
                {
                    continue;
                }

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

        public void Handle(RefreshInstances message)
        {
            RefreshInstances();
        }

        public void Handle(ResetInstances message)
        {
            Instances.Clear();
            foreach (var item in InstanceFinder.AllInstances())
            {
                Instances.Add(instanceDetailsFunc(item));
            }
        }

        void RefreshInstances()
        {
            var missingInstances = InstanceFinder.AllInstances().Where(i => !Instances.Any(existingInstance => existingInstance.Name == i.Name));

            foreach (var item in missingInstances)
            {
                Instances.Add(instanceDetailsFunc(item));
            }
        }

        readonly Func<BaseService, InstanceDetailsViewModel> instanceDetailsFunc;
    }
}