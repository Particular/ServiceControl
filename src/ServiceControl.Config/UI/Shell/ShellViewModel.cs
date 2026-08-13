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

    class ShellViewModel : RxConductor<RxScreen>.OneActive, IHandle<PostRefreshInstances>, IHandle<ResetInstances>
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

        public Task HandleAsync(PostRefreshInstances message, CancellationToken cancellationToken = default) => RefreshInstances(cancellationToken);

        public Task HandleAsync(ResetInstances message, CancellationToken cancellationToken = default) => RefreshInstances(cancellationToken);

        protected override Task OnInitialize(CancellationToken cancellationToken = default) => RefreshInstances(cancellationToken);

        protected override async Task OnActivate(CancellationToken cancellationToken = default)
        {
            await base.OnActivate(cancellationToken);

            BeginCheckForUpdates();
        }

        public async Task RefreshInstances(CancellationToken cancellationToken = default)
        {
            HasInstances = InstanceFinder.AllInstances().Any();

            if (ActiveItem == null || ActiveItem == listInstances || ActiveItem == noInstances)
            {
                if (HasInstances)
                {
                    await ActivateItem(listInstances, cancellationToken);
                }
                else
                {
                    await ActivateItem(noInstances, cancellationToken);
                }
            }
        }

        void LoadAppVersion() => AppVersion = Constants.CurrentVersion;

        void BeginCheckForUpdates()
        {
            if (updateCheckTask is not null)
            {
                return;
            }

            updateCheckTask = CheckForUpdates(CancellationToken.None);

            NotifyOfPropertyChange(nameof(IsCheckingForUpdate));
        }

        async Task CheckForUpdates(CancellationToken cancellationToken)
        {
            try
            {
                var availableUpgradeRelease = await VersionCheckerHelper.GetLatestRelease(AppVersion, cancellationToken);

                if (availableUpgradeRelease.Version == AppVersion)
                {
                    UpdateAvailable = false;
                }
                else
                {
                    AvailableUpgradeReleaseLink = availableUpgradeRelease.Assets.FirstOrDefault()?.Download.ToString();
                    UpdateAvailableText = $"v{availableUpgradeRelease.Version} - Update Available";
                    UpdateAvailable = true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                UpdateAvailable = false;
            }
            finally
            {
                updateCheckTask = null;

                NotifyOfPropertyChange(nameof(UpdateAvailable));
                NotifyOfPropertyChange(nameof(UpdateAvailableText));
                NotifyOfPropertyChange(nameof(AvailableUpgradeReleaseLink));
                NotifyOfPropertyChange(nameof(IsCheckingForUpdate));
            }
        }

        public bool IsCheckingForUpdate => updateCheckTask is not null;

        Task updateCheckTask;
        readonly ListInstancesViewModel listInstances;
        readonly NoInstancesViewModel noInstances;
    }
}
