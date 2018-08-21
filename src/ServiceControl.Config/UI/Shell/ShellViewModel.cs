namespace ServiceControl.Config.UI.Shell
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using Caliburn.Micro;
    using Commands;
    using Events;
    using Extensions;
    using Framework;
    using Framework.Rx;
    using License;
    using ListInstances;
    using NoInstances;
    using ServiceControlInstaller.Engine.Instances;

    class ShellViewModel : RxConductor<RxScreen>.Collection.OneActive, IHandle<RefreshInstances>
    {
        public ShellViewModel(
            NoInstancesViewModel noInstances,
            ListInstancesViewModel listInstances,
            AddServiceControlInstanceCommand addInstance,
            AddMonitoringInstanceCommand addMonitoringInstance,
            OpenViewModelCommand<LicenseViewModel> openLicense,
            IEventAggregator eventAggregator
        )
        {
            this.listInstances = listInstances;
            this.noInstances = noInstances;
            OpenUrl = new OpenURLCommand();
            AddInstance = addInstance;
            AddMonitoringInstance = addMonitoringInstance;
            OpenLicense = openLicense;
            DisplayName = "ServiceControl Config";
            IsModal = false;
            LoadAppVersion();
            CopyrightInfo = $"{DateTime.Now.Year} © Particular Software";

            addInstance.OnCommandExecuting = () => ShowingMenuOverlay = false;
            addMonitoringInstance.OnCommandExecuting = () => ShowingMenuOverlay = false;

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

        public bool ShowRefresh => !ShowOverlay && !IsModal;

        public bool ShowingMenuOverlay { get; set; }

        public RxScreen Overlay { get; set; }

        public string VersionInfo { get; private set; }

        public string CopyrightInfo { get; }

        public bool HasInstances { get; private set; }

        [FeatureToggle(Feature.MonitoringInstances)]
        public bool ShowMonitoringInstances { get; set; }

        public ICommand AddInstance { get; private set; }

        public ICommand AddMonitoringInstance { get; private set; }

        public ICommand OpenLicense { get; private set; }

        public ICommand OpenUrl { get; private set; }

        public ICommand OpenFeedBack { get; set; }

        public ICommand RefreshInstancesCmd { get; }

        public void Handle(RefreshInstances message)
        {
            RefreshInstances();
        }

        protected override void OnInitialize()
        {
            RefreshInstances();
        }

        void RefreshInstances()
        {
            if (ActiveItem != null && !(ActiveItem == listInstances || ActiveItem == noInstances))
            {
                return;
            }

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

        readonly ListInstancesViewModel listInstances;
        readonly NoInstancesViewModel noInstances;
    }
}