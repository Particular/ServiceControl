using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ServiceControl.Config.Xaml.Controls
{
    using System;

    public class FormPathTextBox : TextBox
    {
        static FormPathTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FormPathTextBox), new FrameworkPropertyMetadata(typeof(FormPathTextBox)));

            var originalMetadata = TextProperty.GetMetadata(typeof(TextBox));

            TextProperty.OverrideMetadata(typeof(FormPathTextBox),
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
            DependencyProperty.Register("Header", typeof(string), typeof(FormPathTextBox), new PropertyMetadata(String.Empty));

        public ICommand SelectCommand
        {
            get { return (ICommand)GetValue(SelectCommandProperty); }
            set { SetValue(SelectCommandProperty, value); }
        }

        public static readonly DependencyProperty SelectCommandProperty =
            DependencyProperty.Register("SelectCommand", typeof(ICommand), typeof(FormPathTextBox), new PropertyMetadata(null));

        public string Warning
        {
            get { return (string)GetValue(WarningProperty); }
            set
            {
                SetValue(WarningProperty, value);
            }
        }

        public static readonly DependencyProperty WarningProperty =
            DependencyProperty.Register("Warning", typeof(string), typeof(FormPathTextBox), new PropertyMetadata(String.Empty));
    }
}