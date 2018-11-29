namespace ServiceControl.Config.UI.Shell
{
    using System.Linq;
    using System.Windows.Input;
    using Caliburn.Micro;
    using Commands;
    using Events;
    using Framework.Rx;
    using License;
    using ServiceControlInstaller.Engine.LicenseMgmt;

    class LicenseStatusManager : RxScreen, IHandle<LicenseUpdated>, IHandle<FocusChanged>
    {
        public LicenseStatusManager(OpenViewModelCommand<LicenseViewModel> openLicense)
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
        public string PopupText { get; set; }

        public ICommand OpenLicense { get; set; }

        public bool IsWarning { get; set; }
        public bool IsSerious { get; set; }
        public bool HasFocus { get; set; }

        public void Handle(FocusChanged message)
        {
            HasFocus = message.HasFocus;
        }

        public void Handle(LicenseUpdated message)
        {
            RefreshStatus(false);
        }

        void RefreshStatus(bool updatePopupDisplay)
        {
            var license = LicenseManager.FindLicense();

            var components = new LicenseComponentFactory().CreateComponents(license.Details);

            var mostPressingComponent = components.OrderByDescending(d => d.Importance).FirstOrDefault();

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