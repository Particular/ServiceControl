using System.Windows;

namespace ServiceControl.Config.UI.Shell
{
    using System.Windows.Controls.Primitives;
    using Extensions;

    /// <summary>
    /// Interaction logic for LicensePopupView.xaml
    /// </summary>
    public partial class LicensePopupView
    {
        public LicensePopupView()
        {
            InitializeComponent();
        }

        private void OnCloseButtonClicked(object sender, RoutedEventArgs e)
        {
            TryClosePopup();
        }

        private void TryClosePopup()
        {
            var popup = this.TryFindParent<Popup>();
            if (popup != null)
            {
                popup.IsOpen = false;
            }
        }
    }
}
