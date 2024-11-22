namespace ServiceControl.Config.UI.Shell
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using Caliburn.Micro;
    using Commands;
    using Events;
    using Framework;
    using Framework.Rx;
    using ListInstances;
    using NoInstances;
    using NuGet.Versioning;
    using ServiceControlInstaller.Engine.Instances;

    class ShellViewModel : RxConductor<RxScreen>.OneActive, IHandle<PostRefreshInstances>
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
            CopyrightInfo = $"{DateTime.UtcNow.Year} © Particular Software";
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

        public SemanticVersion AppVersion { get; private set; }

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

        public string UpdateAvailableText { get; set; }

        public string AvailableUpgradeReleaseLink { get; set; }

        public Task HandleAsync(PostRefreshInstances message, CancellationToken cancellationToken) => RefreshInstances();

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
            AppVersion = Constants.CurrentVersion;
        }

        async Task CheckForUpdates()
        {
            // Get the lates upgradble version based on the current version
            // get the json version file from https://s3.us-east-1.amazonaws.com/platformupdate.particular.net/servicecontrol.txt

            var availableUpgradeRelease = await VersionCheckerHelper.GetLatestRelease(AppVersion);

            if (availableUpgradeRelease.Version == AppVersion)
            {
                UpdateAvailable = false;
            }
            else
            {
                AvailableUpgradeReleaseLink = availableUpgradeRelease.Assets.FirstOrDefault().Download.ToString();
                UpdateAvailableText = $"v{availableUpgradeRelease.Version} - Update Available";
                UpdateAvailable = true;
            }

            NotifyOfPropertyChange(nameof(UpdateAvailable));
        }

        readonly ListInstancesViewModel listInstances;
        readonly NoInstancesViewModel noInstances;
    }
}