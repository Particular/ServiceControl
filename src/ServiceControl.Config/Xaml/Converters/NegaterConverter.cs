namespace ServiceControl.Config.Xaml.Converters
{
    using System;
    using System.Globalization;
    using System.Windows.Data;

    public class NegaterConverter : System.Windows.Markup.MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var angle = (double)value;

            return -angle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}