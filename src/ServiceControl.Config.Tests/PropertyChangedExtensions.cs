namespace ServiceControl.Config.Tests
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using NUnit.Framework;

    static class PropertyChangedExtensions
    {
        public static T Collect_changed_properties<T>(this T viewModel, List<string> changedProperties) where T : INotifyPropertyChanged
        {
            changedProperties.Clear();

            viewModel.PropertyChanged += (sender, args) =>
            {
                changedProperties.Add(args.PropertyName);
            };

            return viewModel;
        }

        public static void Was_notified_of_change(this string property, List<string> changedProperties)
        {
            Assert.That(changedProperties, Does.Contain(property));
        }
    }
}