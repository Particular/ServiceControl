using System;
using System.ComponentModel;
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

        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field.GetAttribute<DescriptionAttribute>();

            return attribute != null ? attribute.Description : value.ToString();
        }
    }
}