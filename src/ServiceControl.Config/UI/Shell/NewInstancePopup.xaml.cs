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
            suppressCheckBoxEvents = true;

            CbMonitoring.IsChecked = false;
            MonitoringInfoBox.Visibility = Visibility.Collapsed;
            SetServiceControlCheckboxesEnabled(true);

            CbServiceControl.IsChecked = true;
            CbServicePulse.IsChecked = true;
            CbServicePulse.Opacity = 1.0;
            CbAudit.IsChecked = true;

            NextButton.IsEnabled = true;

            suppressPresetEvents = true;
            PresetFull.IsChecked = true;
            suppressPresetEvents = false;

            suppressCheckBoxEvents = false;
        }

        public bool InstallServiceControl => CbServiceControl.IsChecked == true;
        public bool InstallServicePulse => CbServicePulse.IsChecked == true;
        public bool InstallAudit => CbAudit.IsChecked == true;
        public bool InstallMonitoring => CbMonitoring.IsChecked == true;

        void Preset_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || suppressPresetEvents)
            {
                return;
            }

            suppressCheckBoxEvents = true;

            CbMonitoring.IsChecked = false;
            MonitoringInfoBox.Visibility = Visibility.Collapsed;
            SetServiceControlCheckboxesEnabled(true);

            var (sc, sp, audit) = sender switch
            {
                _ when sender == PresetMinimal => (true, true, false),
                _ when sender == PresetFull => (true, true, true),
                _ when sender == PresetAuditOnly => (false, false, true),
                _ => (false, false, false)
            };

            CbServiceControl.IsChecked = sc;
            CbServicePulse.IsChecked = sp;
            CbAudit.IsChecked = audit;

            UpdateServicePulseState();
            UpdateNextButton();
            suppressCheckBoxEvents = false;
        }

        void ComponentCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (suppressCheckBoxEvents || !IsLoaded)
            {
                return;
            }

            if (sender == CbServiceControl)
            {
                UpdateServicePulseState();
            }

            SyncPresetSelection();
            UpdateNextButton();
        }

        void MonitoringCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            suppressCheckBoxEvents = true;

            CbServiceControl.IsChecked = false;
            CbServicePulse.IsChecked = false;
            CbAudit.IsChecked = false;
            SetServiceControlCheckboxesEnabled(false);
            UpdateServicePulseState();
            SyncPresetSelection();
            MonitoringInfoBox.Visibility = Visibility.Visible;
            UpdateNextButton();

            suppressCheckBoxEvents = false;
        }

        void MonitoringCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            MonitoringInfoBox.Visibility = Visibility.Collapsed;
            PresetFull.IsChecked = true;
        }

        void Next_Click(object sender, RoutedEventArgs e)
        {
            ClosePopup();

            if (DataContext is ShellViewModel shell)
            {
                if (InstallMonitoring)
                {
                    shell.AddMonitoringInstance.Execute(null);
                }
                else
                {
                    _ = shell.LaunchServiceControlAdd(InstallServiceControl, InstallAudit, InstallServicePulse);
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

        void UpdateServicePulseState()
        {
            if (!IsLoaded)
            {
                return;
            }

            var errorChecked = CbServiceControl.IsChecked == true;
            CbServicePulse.IsEnabled = errorChecked;
            CbServicePulse.Opacity = errorChecked ? 1.0 : 0.4;
            if (!errorChecked)
            {
                CbServicePulse.IsChecked = false;
            }
        }

        void SetServiceControlCheckboxesEnabled(bool enabled)
        {
            CbServiceControl.IsEnabled = enabled;
            CbServicePulse.IsEnabled = enabled;
            CbAudit.IsEnabled = enabled;
        }

        void SyncPresetSelection()
        {
            suppressPresetEvents = true;

            switch (InstallServiceControl, InstallServicePulse, InstallAudit)
            {
                case (true, true, true):
                    PresetFull.IsChecked = true;
                    break;
                case (true, true, false):
                    PresetMinimal.IsChecked = true;
                    break;
                case (false, false, true):
                    PresetAuditOnly.IsChecked = true;
                    break;
                default:
                    PresetMinimal.IsChecked = false;
                    PresetFull.IsChecked = false;
                    PresetAuditOnly.IsChecked = false;
                    break;
            }

            suppressPresetEvents = false;
        }

        void UpdateNextButton()
        {
            if (!IsLoaded)
            {
                return;
            }

            NextButton.IsEnabled = InstallServiceControl || InstallAudit || InstallMonitoring;
        }

        bool suppressCheckBoxEvents;
        bool suppressPresetEvents;
    }
}
