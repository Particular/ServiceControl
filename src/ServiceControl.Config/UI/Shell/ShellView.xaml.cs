namespace ServiceControl.Config.UI.Shell
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using Caliburn.Micro;
    using Events;

    partial class ShellView
    {
        public ShellView()
        {
            InitializeComponent();

            Activated += async (s, e) =>
            {
                //IoC.Get<IEventAggregator>().PublishOnUIThread(new RefreshInstances());
                await (Model?.RefreshInstances() ?? Task.CompletedTask);
            };

            Loaded += (sender, args) =>
            {
                var window = GetWindow(licenseWarningPopup);

                if (window == null)
                {
                    return;
                }

                window.LocationChanged += Window_LocationChanged;
                window.SizeChanged += Window_SizeChanged;
                window.LostKeyboardFocus += async (s, e) => await IoC.Get<IEventAggregator>().PublishOnUIThreadAsync(new FocusChanged { HasFocus = false });
                window.GotKeyboardFocus += async (s, e) => await IoC.Get<IEventAggregator>().PublishOnUIThreadAsync(new FocusChanged { HasFocus = true });
            };
        }

        ShellViewModel Model => DataContext as ShellViewModel;

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