namespace ServiceControl.Config.UI.Shell
{
    using System.Windows;
    using Caliburn.Micro;
    using Events;

    partial class ShellView
    {
        public ShellView()
        {
            InitializeComponent();

            Activated += (s, e) => IoC.Get<IEventAggregator>().PublishOnUIThread(new RefreshInstances());

            Loaded += (sender, args) =>
            {
                var window = Window.GetWindow(licenseWarningPopup);

                if (window == null)
                {
                    return;
                }

                window.LocationChanged += Window_LocationChanged;
                window.SizeChanged += Window_SizeChanged;
            };
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MovePopup();
        }

        private void Window_LocationChanged(object sender, System.EventArgs e)
        {
            MovePopup();
        }

        private void MovePopup()
        {
            var offset = licenseWarningPopup.HorizontalOffset;
            licenseWarningPopup.HorizontalOffset = offset + 1;
            licenseWarningPopup.HorizontalOffset = offset;
        }
    }
}