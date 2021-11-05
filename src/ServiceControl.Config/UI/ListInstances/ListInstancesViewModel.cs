namespace ServiceControl.Config.UI.ListInstances
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework.Rx;
    using InstanceDetails;
    using PropertyChanging;
    using ServiceControlInstaller.Engine.Instances;

    class ListInstancesViewModel : RxScreen, IHandle<RefreshInstances>, IHandle<ResetInstances>, IHandle<LicenseUpdated>
    {
        public ListInstancesViewModel(Func<BaseService, InstanceDetailsViewModel> instanceDetailsFunc)
        {
            this.instanceDetailsFunc = instanceDetailsFunc;
            DisplayName = "DEPLOYED INSTANCES";

            Instances = new BindableCollection<InstanceDetailsViewModel>();

            AddMissingInstances();
        }

        public BindableCollection<InstanceDetailsViewModel> OrderedInstances => new BindableCollection<InstanceDetailsViewModel>(Instances.OrderBy(x => x.Name));

        [AlsoNotifyFor(nameof(OrderedInstances))]
        IList<InstanceDetailsViewModel> Instances { get; }

        public Task HandleAsync(LicenseUpdated licenseUpdatedEvent, CancellationToken cancellationToken)
        {
            // on license change inform each instance to refresh the license (1.23.0 and below don't support this)
            foreach (var instance in Instances)
            {
                if (instance.Version <= new Version("1.23.0"))
                {
                    continue;
                }

                if (!instance.HasBrowsableUrl)
                {
                    continue;
                }

                _ = Task.Run(() =>
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
                }, cancellationToken);
            }

            return Task.CompletedTask;
        }

        public Task HandleAsync(RefreshInstances message, CancellationToken cancellationToken)
        {
            AddMissingInstances();
            return Task.CompletedTask;
        }

        public Task HandleAsync(ResetInstances message, CancellationToken cancellationToken)
        {
            Instances.Clear();
            foreach (var item in InstanceFinder.AllInstances().OrderBy(i => i.Name))
            {
                Instances.Add(instanceDetailsFunc(item));
            }
            NotifyOfPropertyChange(nameof(OrderedInstances));
            return Task.CompletedTask;
        }

        void AddMissingInstances()
        {
            var missingInstances = InstanceFinder.AllInstances().Where(i => !Instances.Any(existingInstance => existingInstance.Name == i.Name));

            foreach (var item in missingInstances)
            {
                Instances.Add(instanceDetailsFunc(item));
            }

            NotifyOfPropertyChange(nameof(OrderedInstances));
        }

        readonly Func<BaseService, InstanceDetailsViewModel> instanceDetailsFunc;
    }
}