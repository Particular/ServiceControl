using System.Windows;
using System.Windows.Controls;

namespace ServiceControl.Config.Xaml.Controls
{
    [TemplatePart(Name = "PART_PasswordBox", Type = typeof(PasswordBox))]
    public class FormPasswordBox : Control
    {
        static FormPasswordBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FormPasswordBox), new FrameworkPropertyMetadata(typeof(FormPasswordBox)));
        }

        private PasswordBox passwordBox;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            passwordBox = (PasswordBox)GetTemplateChild("PART_PasswordBox");
            passwordBox.PasswordChanged += (s, e) =>
            {
                if (Text != passwordBox.Password)
                    Text = passwordBox.Password;
            };
        }

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(FormPasswordBox), new PropertyMetadata(""));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(FormPasswordBox),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (dp, e) => ((FormPasswordBox)dp).PasswordTextChanged(e)));

        private void PasswordTextChanged(DependencyPropertyChangedEventArgs e)
        {
            var newPassword = (string)e.NewValue;

            if (passwordBox != null && passwordBox.Password != newPassword)
                passwordBox.Password = newPassword;
        }
    }
}