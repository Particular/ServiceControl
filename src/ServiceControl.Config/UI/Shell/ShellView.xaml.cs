namespace ServiceControl.Config.UI.Shell
{
    using System;
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
                var window = GetWindow(licenseWarningPopup);

                if (window == null)
                {
                    return;
                }

                window.LocationChanged += Window_LocationChanged;
                window.SizeChanged += Window_SizeChanged;
            };
        }

        void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MovePopup();
        }

        void Window_LocationChanged(object sender, EventArgs e)
        {
            MovePopup();
        }

        void MovePopup()
        {
            var offset = licenseWarningPopup.HorizontalOffset;
            licenseWarningPopup.HorizontalOffset = offset + 1;
            licenseWarningPopup.HorizontalOffset = offset;
        }
    }
}