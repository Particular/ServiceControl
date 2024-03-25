namespace ServiceControl.Config.UI.Shell
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Events;
    using Framework.Commands;
    using Framework.Rx;
    using License;
    using ServiceControl.LicenseManagement;
    using ICommand = System.Windows.Input.ICommand;

    class LicenseStatusManager : RxScreen, IHandle<LicenseUpdated>, IHandle<FocusChanged>
    {
        public LicenseStatusManager(AwaitableAbstractCommand<object> openLicense)
        {
            openLicense.OnCommandExecuting = () => ShowPopup = false;
            OpenLicense = openLicense;
            RefreshStatus(true);
        }

        bool forcePopup;

        public bool ShowPopup
        {
            get => (IsSerious || IsWarning) && forcePopup && HasFocus;
            set => forcePopup = value;
        }

        public string PopupHeading { get; set; }
        public string PopupText { get; private set; }

        public ICommand OpenLicense { get; set; }

        public bool IsWarning { get; private set; }
        public bool IsSerious { get; private set; }
        public bool HasFocus { get; private set; }

        public Task HandleAsync(FocusChanged message, CancellationToken cancellationToken)
        {
            HasFocus = message.HasFocus;
            return Task.CompletedTask;
        }

        public Task HandleAsync(LicenseUpdated message, CancellationToken cancellationToken)
        {
            RefreshStatus(false);
            return Task.CompletedTask;
        }

        void RefreshStatus(bool updatePopupDisplay)
        {
            var license = LicenseManager.FindLicense();

            var components = LicenseComponentFactory.CreateComponents(license.Details);

            var mostPressingComponent = components.MaxBy(d => d.Importance);

            IsWarning = mostPressingComponent?.IsWarning ?? false;
            IsSerious = mostPressingComponent?.IsSerious ?? false;
            PopupHeading = mostPressingComponent?.ShortText;
            PopupText = mostPressingComponent?.WarningText;

            if (updatePopupDisplay)
            {
                ShowPopup = IsWarning || IsSerious;
            }
        }
    }
}