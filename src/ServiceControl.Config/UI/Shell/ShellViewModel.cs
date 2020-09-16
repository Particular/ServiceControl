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
    using ListInstances;
    using NoInstances;
    using ServiceControlInstaller.Engine.Instances;

    class ShellViewModel : RxConductor<RxScreen>.Collection.OneActive, IHandle<RefreshInstances>// , IActivate
    {
        public ShellViewModel(
            NoInstancesViewModel noInstances,
            ListInstancesViewModel listInstances,
            AddServiceControlInstanceCommand addInstance,
            AddMonitoringInstanceCommand addMonitoringInstance,
            LicenseStatusManager licenseStatusManager,
            IEventAggregator eventAggregator
        )
        {
            this.listInstances = listInstances;
            this.noInstances = noInstances;
            OpenUrl = new OpenURLCommand();
            AddInstance = addInstance;
            AddMonitoringInstance = addMonitoringInstance;
            LicenseStatusManager = licenseStatusManager;
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

        public string AppVersion { get; private set; }

        public string VersionInfo { get; private set; }

        public string CopyrightInfo { get; }

        public bool HasInstances { get; private set; }

        [FeatureToggle(Feature.MonitoringInstances)]
        public bool ShowMonitoringInstances { get; set; }

        public ICommand AddInstance { get; private set; }

        public ICommand AddMonitoringInstance { get; private set; }

        public ICommand OpenUrl { get; private set; }

        public ICommand OpenFeedBack { get; set; }

        public ICommand RefreshInstancesCmd { get; }

        public LicenseStatusManager LicenseStatusManager { get; private set; }
        
        public bool UpdateAvailable { get; set; }

        public string AvailableUpgradeReleaseLink { get; set; }

        public void Handle(RefreshInstances message)
        {
            RefreshInstances();
        }

        protected override void OnInitialize()
        {
            RefreshInstances();
        }

        protected async override void OnActivate()
        {
            base.OnActivate();

            await CheckForUpdates();
        }

        public void RefreshInstances()
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
            AppVersion = versionParts[0];

            VersionInfo = "v" + AppVersion;

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

        private async Task CheckForUpdates()
        {
            // Get the lates upgradble version based on the current version
            // get the json version file from https://s3.us-east-1.amazonaws.com/platformupdate.particular.net/servicecontrol.txt

            var shortAppVersion = AppVersion.Substring(0, 6);
            
            Release availableUpgradeRelease = await VersionCheckerHelper.GetDownloadUrlForNextVersionToUpdate(shortAppVersion).ConfigureAwait(false);
            
            if (availableUpgradeRelease.Version.ToString() == shortAppVersion)
            {
                UpdateAvailable = false;
                AvailableUpgradeReleaseLink = string.Empty;
            }
            else
            {
                AvailableUpgradeReleaseLink = availableUpgradeRelease.Assets.FirstOrDefault().Download.ToString();
                UpdateAvailable = true;
            }

            NotifyOfPropertyChange(nameof(UpdateAvailable));
        }

        readonly ListInstancesViewModel listInstances;
        readonly NoInstancesViewModel noInstances;
    }
}