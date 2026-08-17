namespace ServiceControl.Config.UI.License
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using Caliburn.Micro;
    using Commands;
    using Events;
    using Framework.Rx;
    using Microsoft.WindowsAPICodePack.Dialogs;
    using ServiceControl.LicenseManagement;

    class LicenseViewModel : RxScreen
    {
        public LicenseViewModel(IEventAggregator eventAggregator) => EventAggregator = eventAggregator;

        public string ApplyLicenseError { get; set; }

        public string ApplyLicenseSuccess { get; set; }

        public List<LicenseComponent> Components { get; set; }

        public ICommand OpenUrl => new OpenURLCommand();

        public ICommand BrowseForFile => new AwaitableSelectPathCommand(OpenLicenseFile, "Select License File", filters: new[] { new CommonFileDialogFilter("License File", "xml") });

        public bool CanExtendTrial { get; set; }

        public string ExtendLicenseUrl { get; set; }

        protected override Task OnActivate(CancellationToken cancellationToken = default)
        {
            RefreshLicenseInfo();
            return Task.CompletedTask;
        }

        void RefreshLicenseInfo()
        {
            license = LicenseManager.FindLicense();

            Components = LicenseComponentFactory.CreateComponents(license.Details).ToList();

            CanExtendTrial = license.Details.IsTrialLicense;

            ExtendLicenseUrl = $"https://particular.net/license/nservicebus?t={(license.IsEvaluationLicense ? 0 : 1)}&p=servicecontrol";
        }

#pragma warning disable PS0018 // Bound to AwaitableSelectPathCommand's Func<string, Task>, which the tokenless ICommand.Execute drives
        async Task OpenLicenseFile(string path)
#pragma warning restore PS0018
        {
            if (LicenseManager.TryImportLicense(path, out var importError))
            {
                ApplyLicenseError = null;
                RefreshLicenseInfo();
                ApplyLicenseSuccess = "License imported successfully";

                await EventAggregator.PublishOnUIThreadAsync(new LicenseUpdated());

            }
            else
            {
                ApplyLicenseSuccess = null;
                ApplyLicenseError = importError;
            }
        }

        DetectedLicense license;
    }
}