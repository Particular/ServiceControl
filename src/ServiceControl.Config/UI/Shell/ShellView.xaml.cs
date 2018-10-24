namespace ServiceControl.Config.UI.Shell
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;
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

            var dpd = DependencyPropertyDescriptor.FromProperty(IsMouseOverProperty, typeof(Image));
            dpd?.AddValueChanged(WarningIcon, OnIsMouseOverChanged);
            dpd?.AddValueChanged(ErrorIcon, OnIsMouseOverChanged);
        }

        void OnIsMouseOverChanged(object sender, EventArgs e)
        {
            if (WarningIcon.IsMouseOver || ErrorIcon.IsMouseOver)
            {
                licenseWarningPopup.IsOpen = true;
            }
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