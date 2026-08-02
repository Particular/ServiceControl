namespace ServiceControl.Config.UI.ListInstances
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;
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

            OpenLogFileCommand = new RelayCommand(OpenLogFile, CanOpenLogFile);
        }

        public ICommand OpenLogFileCommand { get; }

        public BindableCollection<InstanceDetailsViewModel> OrderedInstances => [.. Instances.OrderBy(x => x.Name)];

        public bool HasConfigurationErrors
        {
            get
            {
                var hasErrors = Instances.Any(i => !string.IsNullOrEmpty(i.ConfigurationLoadError));
                return hasErrors;
            }
        }

        public string ConfigurationErrorMessage
        {
            get
            {
                var errorInstances = Instances.Where(i => !string.IsNullOrEmpty(i.ConfigurationLoadError)).ToList();

                if (errorInstances.Count == 0)
                {
                    return null;
                }

                if (errorInstances.Count == 1)
                {
                    var instance = errorInstances[0];
                    return $"{instance.Name} instance cannot be loaded due to XML configuration error.";
                }

                var names = string.Join(", ", errorInstances.Select(i => i.Name));
                return $"Multiple instances ({names}) cannot be loaded due to XML configuration errors.";
            }
        }

        public IEnumerable<InstanceDetailsViewModel> InstancesWithConfigErrors => Instances.Where(i => !string.IsNullOrEmpty(i.ConfigurationLoadError));

        [AlsoNotifyFor(nameof(OrderedInstances), nameof(HasConfigurationErrors), nameof(ConfigurationErrorMessage), nameof(InstancesWithConfigErrors))]
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
            NotifyOfPropertyChange(nameof(Instances));
        }

        async void AddAndRemoveInstances()
        {
            var toRemove = Instances.Where(instance => !instance.Exists());
            foreach (var instance in toRemove)
            {
                await instance.TryCloseAsync();
            }
            Instances.RemoveMany(toRemove);

            // Get fresh instances from disk (with updated configurations)
            var allFreshInstances = InstanceFinder.AllInstances();

            // Update existing instances with fresh configuration data
            foreach (var existingInstance in Instances)
            {
                var freshInstance = allFreshInstances.FirstOrDefault(i => i.Name == existingInstance.Name);
                if (freshInstance != null)
                {
                    existingInstance.UpdateServiceInstance(freshInstance);
                }
            }

            var missingInstances = allFreshInstances.Where(i => !Instances.Any(existingInstance => existingInstance.Name == i.Name));

            foreach (var item in missingInstances)
            {
                Instances.Add(instanceDetailsFunc(item));
            }

            Validations.RefreshInstances();

            NotifyOfPropertyChange(nameof(Instances));
        }

        void OpenLogFile(object parameter)
        {
            if (parameter is string logFilePath && !string.IsNullOrEmpty(logFilePath))
            {
                try
                {
                    if (File.Exists(logFilePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = logFilePath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        // If the log file doesn't exist, try to open the directory
                        var directory = Path.GetDirectoryName(logFilePath);
                        if (Directory.Exists(directory))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = directory,
                                UseShellExecute = true
                            });
                        }
                    }
                }
                catch
                {
                    // Ignore errors opening the file
                }
            }
        }

        bool CanOpenLogFile(object parameter)
        {
            return parameter is string logFilePath && !string.IsNullOrEmpty(logFilePath);
        }

        readonly Func<BaseService, InstanceDetailsViewModel> instanceDetailsFunc;

        class RelayCommand : ICommand
        {
            readonly Action<object> execute;
            readonly Func<object, bool> canExecute;

            public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
            {
                this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
                this.canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => canExecute == null || canExecute(parameter);

            public void Execute(object parameter) => execute(parameter);

            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }
        }
    }
}