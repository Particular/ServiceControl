namespace ServiceControl.Config.Xaml.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;

    public class IsLastItemInContainerVisibilityConverter : IValueConverter
    {
        public Visibility LastItemVisibility { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var item = (DependencyObject)value;
            if (item != null)
            {
                var itemsControl = ItemsControl.ItemsControlFromItemContainer(item);
                if (itemsControl?.ItemContainerGenerator?.IndexFromContainer(item) == itemsControl?.Items.Count - 1)
                {
                    return LastItemVisibility;
                }
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}