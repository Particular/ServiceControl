namespace ServiceControl.Config.UI.License
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;
    using Caliburn.Micro;
    using Commands;
    using Events;
    using Framework.Rx;
    using Microsoft.WindowsAPICodePack.Dialogs;
    using ServiceControlInstaller.Engine.LicenseMgmt;

    class LicenseViewModel : RxScreen
    {
        public LicenseViewModel(IEventAggregator eventAggregator)
        {
            EventAggregator = eventAggregator;
        }

        public string ApplyLicenseError { get; set; }

        public string ApplyLicenseSuccess { get; set; }

        public List<LicenseComponent> Components { get; set; }

        public ICommand OpenUrl => new OpenURLCommand();

        public ICommand BrowseForFile => new SelectPathCommand(OpenLicenseFile, "Select License File", filters: new[] {new CommonFileDialogFilter("License File", "xml")});

        public bool CanExtendTrial { get; set; }

        protected override void OnActivate()
        {
            RefreshLicenseInfo();
        }

        void RefreshLicenseInfo()
        {
            license = LicenseManager.FindLicense();

            Components = new LicenseComponentFactory().CreateComponents(license.Details).ToList();

            CanExtendTrial = license.Details.IsTrialLicense;
        }

        void OpenLicenseFile(string path)
        {
            if (LicenseManager.TryImportLicense(path, out var importError))
            {
                ApplyLicenseError = null;
                RefreshLicenseInfo();
                ApplyLicenseSuccess = "License imported successfully";
                EventAggregator.PublishOnUIThread(new LicenseUpdated());
            }
            else
            {
                ApplyLicenseSuccess = null;
                ApplyLicenseError = importError;
            }
        }

        IEventAggregator EventAggregator;

        DetectedLicense license;
    }
}