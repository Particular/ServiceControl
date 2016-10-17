namespace ServiceControl.Config.UI.License
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Windows.Input;
    using Caliburn.Micro;
    using Microsoft.WindowsAPICodePack.Dialogs;
    using ServiceControl.Config.Commands;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControlInstaller.Engine.LicenseMgmt;

    class LicenseViewModel : RxScreen
    {
        private IEventAggregator EventAggregator;

        public LicenseViewModel(IEventAggregator eventAggregator)
        {
            EventAggregator = eventAggregator;
        }


        protected override void OnActivate()
        {
            RefreshLicenseInfo();
        }

        public string LicenseWarning { get; set; }

        public string ApplyLicenseError { get; set; }

        public string ApplyLicenseSuccess { get; set; }

        public Dictionary<string, string> LicenseInfo { get; set; }

        public ICommand OpenUrl => new OpenURLCommand();

        public ICommand BrowseForFile =>  new SelectPathCommand(OpenLicenseFile, "Select License File", filters: new[] { new CommonFileDialogFilter("License File", "xml") });

        DetectedLicense license;

        void RefreshLicenseInfo()
        {
            var details = new Dictionary<string, string>();
            LicenseWarning = null;
            license = LicenseManager.FindLicense();
            
            details.Add("License Type:", license.Details.IsTrialLicense ? "Trial License" : license.Details.LicenseType);
            if (!license.Details.IsTrialLicense)
            {
                details.Add("License Edition:", license.Details.Edition);
            }
            details.Add("Licensed To:", license.Details.RegisteredTo);
            if (license.Details.ExpirationDate.HasValue)
            {
                var expirationDate = license.Details.ExpirationDate.Value;
                details.Add("License Expiration:", expirationDate.ToString("dd MMMM yyyy"));
                if (HasExpired(expirationDate))
                {
                    LicenseWarning = "This license has expired";
                }
                else if (!license.Details.IsTrialLicense && WithinLicenseWarningRange(expirationDate))
                {
                    LicenseWarning = "This license will expire soon";
                }
            }
            LicenseInfo = details;
        }

        void OpenLicenseFile(string path)
        {
            string importError;
            if (LicenseManager.TryImportLicense(path, out importError))
            {
                ApplyLicenseError = null;
                RefreshLicenseInfo();
                ApplyLicenseSuccess = "License imported successfully";
                EventAggregator.PublishOnUIThread(new LicenseUpdated());
            }
            else
            {
                ApplyLicenseSuccess = null;
                ApplyLicenseError = $"{importError}{Environment.NewLine}'{Path.GetFileName(path)}' was not imported";
            }
        }

        bool WithinLicenseWarningRange(DateTime licenseDate)
        {
            return (licenseDate > DateTime.Now) & (licenseDate < DateTime.Now.AddDays(30));
        }

        bool HasExpired(DateTime licenseDate)
        {
            return licenseDate < DateTime.Now;
        }
    }
}