using System.Windows;
using System.Windows.Controls.Primitives;

namespace ServiceControl.Config.Xaml.Controls
{
    using System;

    public class FormToggleButton : ToggleButton
    {
        static FormToggleButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FormToggleButton), new FrameworkPropertyMetadata(typeof(FormToggleButton)));
        }

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(FormToggleButton), new PropertyMetadata(String.Empty));
    }
}