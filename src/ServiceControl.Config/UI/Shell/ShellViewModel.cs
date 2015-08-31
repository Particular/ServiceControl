namespace ServiceControl.Config.UI.Shell
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Input;
    using Caliburn.Micro;
    using ServiceControl.Config.Commands;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Extensions;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControl.Config.UI.ListInstances;
    using ServiceControl.Config.UI.NoInstances;
    using ServiceControlInstaller.Engine.Instances;

    internal class ShellViewModel : RxConductor<RxScreen>.Collection.OneActive, IHandle<RefreshInstances>
    {
        private readonly ListInstancesViewModel listInstances;
        private readonly NoInstancesViewModel noInstances;

        public ShellViewModel(
            NoInstancesViewModel noInstances,
            ListInstancesViewModel listInstances,
            AddInstanceCommand addInstance,
            OpenViewModelCommand<License.LicenseViewModel> openLicense,
            IEventAggregator eventAggregator
            )
        {
            this.listInstances = listInstances;
            this.noInstances = noInstances;
            OpenUrl = new OpenURLCommand();
            AddInstance = addInstance;
            OpenLicense = openLicense;
            DisplayName = "ServiceControl Config";
            IsModal = false;

            LoadAppVersion();

            RefreshInstancesCmd = Command.Create(() => eventAggregator.PublishOnUIThread(new RefreshInstances()));
        }

        public object ActiveContext { get; set; }

        public bool IsModal { get; set; }

        public bool ShowOverlay { get { return Overlay != null; } }

        public bool ShowRefresh { get { return HasInstances && !ShowOverlay && !IsModal; } }

        public RxScreen Overlay { get; set; }

        public string VersionInfo { get; private set; }

        public bool HasInstances { get; private set; }

        public ICommand AddInstance { get; private set; }

        public ICommand OpenLicense { get; private set; }

        public ICommand OpenUrl { get; private set; }

        public ICommand OpenFeedBack { get; set; }

        public ICommand RefreshInstancesCmd { get; private set; }

        protected override void OnInitialize()
        {
            RefreshInstances();
        }

        public void Handle(RefreshInstances message)
        {
            RefreshInstances();
        }

        private void RefreshInstances()
        {
            if (ActiveItem != null && ActiveItem != listInstances && ActiveItem != noInstances)
                return;

            HasInstances = ServiceControlInstance.Instances().Any();

            if (HasInstances)
            {
                ActivateItem(listInstances);
            }
            else
            {
                ActivateItem(noInstances);
            }
        }

        void LoadAppVersion()
        {
            var assemblyInfo = typeof(App).Assembly.GetAttribute<AssemblyInformationalVersionAttribute>();
            var version = assemblyInfo != null ? assemblyInfo.InformationalVersion : "Unknown Version";
            var versionParts = version.Split('+');
            var appVersion = versionParts[0];

            VersionInfo = "v" + appVersion;

            var metadata = versionParts.Last();
            var parts = metadata.Split('.');
            var shaIndex = parts.IndexOf("Sha", StringComparer.InvariantCultureIgnoreCase);
            if (shaIndex != -1 && parts.Length > shaIndex + 1)
            {
                var shaValue = parts[shaIndex + 1];
                var shortCommitHash = shaValue.Substring(0, 7);

                VersionInfo += " / " + shortCommitHash;
            }
        }
    }
}