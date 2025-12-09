namespace ServiceControl.Config.Xaml.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Input;

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

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (TryHandleSpecialTextKey(e))
            {
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        bool TryHandleSpecialTextKey(KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0)
            {
                return false;
            }

            var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            var c = MapKeyToChar(e.Key, shift);
            if (c is null)
            {
                return false;
            }

            e.Handled = true;
            if (!IsReadOnly && IsEnabled)
            {
                var composition = new TextComposition(InputManager.Current, this, c.Value.ToString());
                var args = new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
                {
                    RoutedEvent = TextCompositionManager.TextInputEvent
                };
                RaiseEvent(args);
            }

            return true;
        }

        static char? MapKeyToChar(Key key, bool shift)
        {
            return key switch
            {
                Key.OemMinus => shift ? '_' : '-',
                Key.OemPlus => shift ? '+' : '=',
                Key.Add => '+',
                Key.Subtract => '-',
                _ => null
            };
        }

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public string Warning
        {
            get { return (string)GetValue(WarningProperty); }
            set { SetValue(WarningProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(FormTextBox), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty WarningProperty =
            DependencyProperty.Register("Warning", typeof(string), typeof(FormTextBox), new PropertyMetadata(string.Empty));
    }
}