namespace ServiceControl.Config.Xaml.Controls
{
    using System.Windows;
    using System.Windows.Controls;

    public class FormWarningTextBlock : Control
    {
        static FormWarningTextBlock()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FormWarningTextBlock), new FrameworkPropertyMetadata(typeof(FormWarningTextBlock)));
        }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty = 
            DependencyProperty.Register("Text", typeof(string), typeof(FormWarningTextBlock), new PropertyMetadata(string.Empty));
    }
}