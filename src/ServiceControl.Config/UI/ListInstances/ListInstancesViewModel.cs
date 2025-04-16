namespace ServiceControl.Config.UI.ListInstances
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using DynamicData;
    using Events;
    using Framework.Rx;
    using InstanceDetails;
    using NuGet.Versioning;
    using PropertyChanging;
    using ServiceControl.Config.Extensions;
    using ServiceControlInstaller.Engine.Instances;

    class ListInstancesViewModel : RxScreen, IHandle<RefreshInstances>, IHandle<ResetInstances>, IHandle<LicenseUpdated>
    {
        public ListInstancesViewModel(Func<BaseService, InstanceDetailsViewModel> instanceDetailsFunc)
        {
            this.instanceDetailsFunc = instanceDetailsFunc;
            DisplayName = "DEPLOYED INSTANCES";

            Instances = [];

            AddAndRemoveInstances();
        }

        public BindableCollection<InstanceDetailsViewModel> OrderedInstances => [.. Instances.OrderBy(x => x.Name)];

        [AlsoNotifyFor(nameof(OrderedInstances))]
        IList<InstanceDetailsViewModel> Instances { get; }

        public Task HandleAsync(LicenseUpdated licenseUpdatedEvent, CancellationToken cancellationToken)
        {
            // on license change inform each instance to refresh the license (1.23.0 and below don't support this)
            foreach (var instance in Instances)
            {
                if (instance.Version <= new SemanticVersion(1, 23, 0))
                {
                    continue;
                }

                if (!instance.HasBrowsableUrl)
                {
                    continue;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var http = new HttpClient();
                        http.Timeout = TimeSpan.FromSeconds(2);
                        await http.GetAsync($"{instance.BrowsableUrl}license?refresh=true");
                    }
                    catch
                    {
                        // Ignored
                    }
                }, cancellationToken);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Should be only subscriber for RefreshInstances so that add/removes can happen in the list
        /// before the PostRefreshInstances handlers do all their rebinding. That way, deleting an instance
        /// in PowerShell won't cause an error from a deleted instance viewmodel trying to refresh itself.
        /// </summary>
        public async Task HandleAsync(RefreshInstances message, CancellationToken cancellationToken)
        {
            AddAndRemoveInstances();
            await EventAggregator.PublishOnUIThreadAsync(new PostRefreshInstances(), cancellationToken);
        }

        public async Task HandleAsync(ResetInstances message, CancellationToken cancellationToken)
        {
            foreach (var instance in Instances)
            {
                await instance.TryCloseAsync(true);
            }

            Instances.Clear();

            foreach (var item in InstanceFinder.AllInstances().OrderBy(i => i.Name))
            {
                Instances.Add(instanceDetailsFunc(item));
            }
            NotifyOfPropertyChange(nameof(OrderedInstances));
        }

        async void AddAndRemoveInstances()
        {
            var toRemove = Instances.Where(instance => !instance.Exists());
            foreach (var instance in toRemove)
            {
                await instance.TryCloseAsync();
            }
            Instances.RemoveMany(toRemove);

            var missingInstances = InstanceFinder.AllInstances().Where(i => !Instances.Any(existingInstance => existingInstance.Name == i.Name));

            foreach (var item in missingInstances)
            {
                Instances.Add(instanceDetailsFunc(item));
            }

            Validations.RefreshInstances();

            NotifyOfPropertyChange(nameof(OrderedInstances));
        }

        readonly Func<BaseService, InstanceDetailsViewModel> instanceDetailsFunc;
    }
}