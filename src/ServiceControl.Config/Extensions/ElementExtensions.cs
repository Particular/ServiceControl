namespace ServiceControl.Config.Extensions
{
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Media;

    public static class ElementExtensions
    {
        public static T TryFindChild<T>(this DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            T foundChild = null;
            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);

            for (var i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (!(child is T typedChild))
                {
                    foundChild = TryFindChild<T>(child, childName);
                    if (foundChild != null)
                    {
                        break;
                    }
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    if (child is FrameworkElement frameworkElement && frameworkElement.Name == childName)
                    {
                        foundChild = typedChild;
                        break;
                    }
                }
                else
                {
                    foundChild = typedChild;
                    break;
                }
            }
            return foundChild;
        }

        public static T TryFindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            var parentObject = GetParentObject(child);
            if (parentObject == null)
            {
                return null;
            }

            if (parentObject is T parent)
            {
                return parent;
            }

            return TryFindParent<T>(parentObject);
        }

        public static DependencyObject GetParentObject(this DependencyObject child)
        {
            if (child == null)
            {
                return null;
            }

            if (child is ContentElement contentElement)
            {
                var parent = ContentOperations.GetParent(contentElement);
                if (parent != null)
                {
                    return parent;
                }

                return contentElement is FrameworkContentElement fce ? fce.Parent : null;
            }

            if (child is FrameworkElement frameworkElement)
            {
                var parent = frameworkElement.Parent;
                if (parent != null)
                {
                    return parent;
                }
            }

            return VisualTreeHelper.GetParent(child);
        }

        public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
            {
                yield break;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T concreteChild)
                {
                    yield return concreteChild;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        public static T GetResource<T>(this DependencyObject element, object key)
        {
            for (var dependencyObject = element; dependencyObject != null; dependencyObject = LogicalTreeHelper.GetParent(dependencyObject) ?? VisualTreeHelper.GetParent(dependencyObject))
            {
                if (dependencyObject is FrameworkElement frameworkElement)
                {
                    if (frameworkElement.Resources.Contains(key))
                    {
                        return (T)frameworkElement.Resources[key];
                    }
                }
                else
                {
                    if (dependencyObject is FrameworkContentElement frameworkContentElement && frameworkContentElement.Resources.Contains(key))
                    {
                        return (T)frameworkContentElement.Resources[key];
                    }
                }
            }
            if (Application.Current != null && Application.Current.Resources.Contains(key))
            {
                return (T)Application.Current.Resources[key];
            }
            return default;
        }
    }
}
