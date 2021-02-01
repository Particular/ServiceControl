namespace ServiceControl.Config.UI.Shell
{
    using System.Windows;
    using System.Windows.Controls.Primitives;
    using ServiceControl.Config.Extensions;

    /// <summary>
    /// Interaction logic for LicensePopupView.xaml
    /// </summary>
    public partial class LicensePopupView
    {
        public LicensePopupView()
        {
            InitializeComponent();
        }

        void OnCloseButtonClicked(object sender, RoutedEventArgs e)
        {
            TryClosePopup();
        }

        void TryClosePopup()
        {
            var popup = this.TryFindParent<Popup>();
            if (popup != null)
            {
                popup.IsOpen = false;
            }
        }
    }
}
