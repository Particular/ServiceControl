namespace ServiceControl.Config.UI.Shell
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
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

    class ShellViewModel : RxConductor<RxScreen>.OneActive, IHandle<RefreshInstances>
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

            RefreshInstancesCmd = Command.Create(async () =>
            {
                await eventAggregator.PublishOnUIThreadAsync(new RefreshInstances());
                // Used to "blink" the refresh button to indicate the refresh actually ran.
                await Task.Delay(500);
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

        public Task HandleAsync(RefreshInstances message, CancellationToken cancellationToken) => RefreshInstances();

        protected override Task OnInitialize() => RefreshInstances();

        protected override async Task OnActivate()
        {
            await base.OnActivate();

            await CheckForUpdates();
        }

        public async Task RefreshInstances()
        {
            if (ActiveItem != null && !(ActiveItem == listInstances || ActiveItem == noInstances))
            {
                return;
            }

            HasInstances = InstanceFinder.AllInstances().Any();

            if (HasInstances)
            {
                await ActivateItem(listInstances);
            }
            else
            {
                await ActivateItem(noInstances);
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

        async Task CheckForUpdates()
        {
            // Get the lates upgradble version based on the current version
            // get the json version file from https://s3.us-east-1.amazonaws.com/platformupdate.particular.net/servicecontrol.txt
            var shortAppVersion = AppVersion.Split('-').First();

            var availableUpgradeRelease = await VersionCheckerHelper.GetLatestRelease(shortAppVersion).ConfigureAwait(false);

            if (availableUpgradeRelease.Version.ToString() == shortAppVersion)
            {
                UpdateAvailable = false;
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
