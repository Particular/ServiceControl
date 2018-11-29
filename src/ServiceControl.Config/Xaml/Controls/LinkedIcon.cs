namespace ServiceControl.Config.Xaml.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;

    public class LinkedIcon : ContentControl
    {
        static LinkedIcon()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LinkedIcon), new FrameworkPropertyMetadata(typeof(LinkedIcon)));
        }

        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
            "ImageSource", typeof(ImageSource), typeof(LinkedIcon), new PropertyMetadata(null));

        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(LinkedIcon), new PropertyMetadata(default(string)));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            "Command", typeof(ICommand), typeof(LinkedIcon), new PropertyMetadata(default(ICommand)));

        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }
    }
}
