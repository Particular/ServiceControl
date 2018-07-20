namespace ServiceControl.Config.Xaml.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Interop;
    using Native;

    [TemplatePart(Name = "PART_Min", Type = typeof(Button))]
    [TemplatePart(Name = "PART_Max", Type = typeof(Button))]
    [TemplatePart(Name = "PART_Clo", Type = typeof(Button))]
    public class CleanWindow : Window
    {
        static CleanWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CleanWindow), new FrameworkPropertyMetadata(typeof(CleanWindow)));
        }


        public bool CloseOnEscape
        {
            get { return (bool)GetValue(CloseOnEscapeProperty); }
            set { SetValue(CloseOnEscapeProperty, value); }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild("PART_Clo") is Button closeButton)
            {
                closeButton.Click += CloseButton_Click;
            }

            if (GetTemplateChild("PART_Min") is Button minimizeButton)
            {
                minimizeButton.Click += MinimizeButton_Click;
            }

            if (GetTemplateChild("PART_Max") is Button maximizeButton)
            {
                maximizeButton.Click += MaximizeButton_Click;
            }
        }

        void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = ((HwndSource)HwndSource.FromVisual(this)).Handle;
            UnsafeNativeMethods.ShowWindow(hwnd, ShowWindowCommands.Minimize);
        }

        void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = ((HwndSource)HwndSource.FromVisual(this)).Handle;
            UnsafeNativeMethods.ShowWindow(hwnd, WindowState == WindowState.Normal ? ShowWindowCommands.Maximize : ShowWindowCommands.Normal);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.RightButton != MouseButtonState.Pressed &&
                e.MiddleButton != MouseButtonState.Pressed &&
                e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }

            base.OnMouseDown(e);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (!CloseOnEscape)
            {
                return;
            }

            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static readonly DependencyProperty CloseOnEscapeProperty =
            DependencyProperty.Register("CloseOnEscape", typeof(bool), typeof(CleanWindow), new PropertyMetadata(true));
    }
}