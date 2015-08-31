using System.Windows;
using System.Windows.Controls;

namespace ServiceControl.Config.Xaml.Controls
{
    [TemplatePart(Name = "PART_On", Type = typeof(RadioButton))]
    [TemplatePart(Name = "PART_Off", Type = typeof(RadioButton))]
    public class FormCheckBox : CheckBox
    {
        static FormCheckBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FormCheckBox), new FrameworkPropertyMetadata(typeof(FormCheckBox)));
        }

        private RadioButton onButton;
        private RadioButton offButton;

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(FormCheckBox), new PropertyMetadata(""));

        public override void OnApplyTemplate()
        {
            onButton = (RadioButton)GetTemplateChild("PART_On");
            offButton = (RadioButton)GetTemplateChild("PART_Off");

            onButton.Checked += OnButton_Checked;
            offButton.Checked += OffButton_Checked;


        }

        private void OffButton_Checked(object sender, RoutedEventArgs e)
        {
           this.IsChecked = !offButton.IsChecked;
        }

        private void OnButton_Checked(object sender, RoutedEventArgs e)
        {
            this.IsChecked = onButton.IsChecked;
        }

        protected override void OnChecked(RoutedEventArgs e)
        {
            base.OnChecked(e);

            AssignChecked();
        }

        void AssignChecked()
        {
            if (onButton == null || offButton == null)
            {
                return;
            }

            onButton.IsChecked = this.IsChecked;
            offButton.IsChecked = !this.IsChecked;
        }
    }
}