using System.Windows;
using System.Windows.Controls;

namespace ServiceControl.Config.Xaml.Controls
{
    using System;

    public class FormComboBox : ComboBox
    {
        static FormComboBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FormComboBox), new FrameworkPropertyMetadata(typeof(FormComboBox)));
        }

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(FormComboBox), new PropertyMetadata(String.Empty));
    }
}