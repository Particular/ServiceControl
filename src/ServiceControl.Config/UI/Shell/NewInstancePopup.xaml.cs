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
            CbAudit.IsChecked = true;

            CbServicePulse.Opacity = 1.0;
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
            if (CbServiceControl == null || suppressPresetEvents)
            {
                return;
            }

            suppressCheckBoxEvents = true;

            CbMonitoring.IsChecked = false;
            MonitoringInfoBox.Visibility = Visibility.Collapsed;
            SetServiceControlCheckboxesEnabled(true);

            if (sender == PresetMinimal)
            {
                CbServiceControl.IsChecked = true;
                CbServicePulse.IsChecked = true;
                CbAudit.IsChecked = false;
            }
            else if (sender == PresetFull)
            {
                CbServiceControl.IsChecked = true;
                CbServicePulse.IsChecked = true;
                CbAudit.IsChecked = true;
            }
            else if (sender == PresetAuditOnly)
            {
                CbServiceControl.IsChecked = false;
                CbServicePulse.IsChecked = false;
                CbAudit.IsChecked = true;
            }

            UpdateServicePulseEnabled();
            UpdateNextButton();
            suppressCheckBoxEvents = false;
        }

        void ServiceControlCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (suppressCheckBoxEvents || !IsLoaded)
            {
                return;
            }

            UpdateServicePulseEnabled();
            SyncPresetSelection();
            UpdateNextButton();
        }

        void ComponentCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (suppressCheckBoxEvents || !IsLoaded)
            {
                return;
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

        void UpdateServicePulseEnabled()
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
            var sc = CbServiceControl.IsChecked == true;
            var sp = CbServicePulse.IsChecked == true;
            var audit = CbAudit.IsChecked == true;

            suppressPresetEvents = true;

            if (sc && sp && audit)
            {
                PresetFull.IsChecked = true;
            }
            else if (sc && sp && !audit)
            {
                PresetMinimal.IsChecked = true;
            }
            else if (!sc && !sp && audit)
            {
                PresetAuditOnly.IsChecked = true;
            }
            else
            {
                PresetMinimal.IsChecked = false;
                PresetFull.IsChecked = false;
                PresetAuditOnly.IsChecked = false;
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
