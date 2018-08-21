// ReSharper disable HeuristicUnreachableCode

namespace ServiceControl.Config.Extensions
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    public static class VisualTreeExtensions
    {
        public static Control FindControlWithError(this DependencyObject parent)
        {
            // Confirm parent and childName are valid.
            if (parent == null)
            {
                return null;
            }

            Control foundChild = null;

            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                // If the child is not of the request child type child
                var childType = child;
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (childType == null)
                {
                    // recursively drill down the tree
                    foundChild = FindControlWithError(child);

                    // If the child is found, break so we do not overwrite the found child.
                    if (foundChild != null)
                    {
                        break;
                    }
                }
                else
                {
                    // If the child is in error
                    if (child is FrameworkElement frameworkElement && Validation.GetHasError(frameworkElement))
                    {
                        foundChild = (Control)child;
                        break;
                    }

                    // recursively drill down the tree
                    foundChild = FindControlWithError(child);

                    // If the child is found, break so we do not overwrite the found child.
                    if (foundChild != null)
                    {
                        break;
                    }
                }
            }

            return foundChild;
        }
    }
}