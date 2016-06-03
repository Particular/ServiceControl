using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ServiceControl.Config.Xaml.Controls
{
    using System;

    public class FormTextBox : TextBox
    {
        static FormTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FormTextBox), new FrameworkPropertyMetadata(typeof(FormTextBox)));

            var originalMetadata = TextProperty.GetMetadata(typeof(TextBox));

            TextProperty.OverrideMetadata(typeof(FormTextBox),
                new FrameworkPropertyMetadata(
                    originalMetadata.DefaultValue,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal,
                    originalMetadata.PropertyChangedCallback,
                    originalMetadata.CoerceValueCallback,
                    true,
                    UpdateSourceTrigger.PropertyChanged));
        }

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(FormTextBox), new PropertyMetadata(String.Empty));

        public string Warning
        {
            get { return (string)GetValue(WarningProperty); }
            set
            {
                SetValue(WarningProperty, value);
            }
        }

        public static readonly DependencyProperty WarningProperty =
            DependencyProperty.Register("Warning", typeof(string), typeof(FormTextBox), new PropertyMetadata(String.Empty));
    }
}