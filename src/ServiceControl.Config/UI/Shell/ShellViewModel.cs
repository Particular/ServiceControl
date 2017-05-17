namespace ServiceControl.Config.UI.Shell
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
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
            CopyrightInfo = $"{DateTime.Now.Year} © Particular Software";

            RefreshInstancesCmd = Command.Create(() =>
            {
                eventAggregator.PublishOnUIThread(new RefreshInstances());
                // Used to "blink" the refresh button to indicate the refresh actually ran.
                return Task.Delay(500);
            });
        }

        public object ActiveContext { get; set; }

        public bool IsModal { get; set; }

        public bool ShowOverlay => Overlay != null;

        public bool ShowRefresh => HasInstances && !ShowOverlay && !IsModal;

        public RxScreen Overlay { get; set; }

        public string VersionInfo { get; private set; }

        public string CopyrightInfo { get; }

        public bool HasInstances { get; private set; }

        public ICommand AddInstance { get; }

        public ICommand OpenLicense { get; }

        public ICommand OpenUrl { get; }

        public ICommand OpenFeedBack { get; set; }

        public ICommand RefreshInstancesCmd { get; }

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

            HasInstances = InstanceFinder.AllInstances().Any();

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