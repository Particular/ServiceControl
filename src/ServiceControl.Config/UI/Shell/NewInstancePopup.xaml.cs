namespace ServiceControl.Config.UI.Shell
{
    using System.Windows;
    using System.Windows.Controls.Primitives;

    public partial class NewInstanceOverlay
    {
        public NewInstanceOverlay()
        {
            InitializeComponent();
            IsVisibleChanged += OnIsVisibleChanged;
        }

        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                ResetToDefaults();
            }
        }

        void ResetToDefaults()
        {
            installServicePulse = true;

            // Assigning IsChecked when it is already true raises no Checked event, so the
            // mode is set directly rather than relying on the radio button to report it.
            selectedMode = SetupMode.ErrorAndAudit;
            ModeErrorAndAudit.IsChecked = true;

            UpdateComponentList();
        }

        bool InstallServiceControl => selectedMode.InstallsServiceControl();

        bool InstallAudit => selectedMode.InstallsAudit();

        bool InstallMonitoring => selectedMode.InstallsMonitoring();

        bool InstallServicePulse => selectedMode.InstallsServicePulse(installServicePulse);

        void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            selectedMode = sender switch
            {
                _ when sender == ModeErrorHandling => SetupMode.ErrorHandling,
                _ when sender == ModeAuditOnly => SetupMode.AuditOnly,
                _ when sender == ModeMonitoringOnly => SetupMode.MonitoringOnly,
                _ => SetupMode.ErrorAndAudit
            };

            UpdateComponentList();
        }

        void ServicePulse_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            installServicePulse = CbServicePulse.IsChecked == true;
        }

        void UpdateComponentList()
        {
            ServiceControlRow.Visibility = Visible(InstallServiceControl);
            AuditRow.Visibility = Visible(InstallAudit);
            MonitoringRow.Visibility = Visible(InstallMonitoring);

            // ServicePulse is hosted by the error instance, so it is only on offer in the
            // scenarios that install one. The choice is remembered across scenario changes.
            CbServicePulse.Visibility = Visible(InstallServiceControl);
            CbServicePulse.IsChecked = installServicePulse;
        }

        static Visibility Visible(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

        // async void is deliberate: this is an event handler, so awaiting here routes any
        // failure to the dispatcher's unhandled exception handler rather than dropping it
        // on an unobserved task.
        async void Next_Click(object sender, RoutedEventArgs e)
        {
            ClosePopup();

            if (DataContext is ShellViewModel shell)
            {
                if (InstallMonitoring)
                {
                    shell.LaunchMonitoringAdd();
                }
                else
                {
                    await shell.LaunchServiceControlAdd(InstallServiceControl, InstallAudit, InstallServicePulse);
                }
            }
        }

        void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ClosePopup();
        }

        void ClosePopup()
        {
            if (Parent is Popup popup)
            {
                popup.IsOpen = false;
            }
        }

        SetupMode selectedMode = SetupMode.ErrorAndAudit;
        bool installServicePulse = true;
    }
}
