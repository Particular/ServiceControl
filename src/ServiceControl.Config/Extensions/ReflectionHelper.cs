using System.Reflection;

namespace ServiceControl.Config.Extensions
{
    public static class ReflectionHelper
    {
        public static T GetAttribute<T>(this ICustomAttributeProvider provider, bool inherit = false)
            where T : class
        {
            var customAttributes = provider.GetCustomAttributes(typeof(T), inherit);
            if (customAttributes.Length > 0)
                return (T)customAttributes[0];

            return null;
        }

    }
}