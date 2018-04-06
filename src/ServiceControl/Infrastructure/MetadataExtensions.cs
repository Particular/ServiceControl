namespace ServiceControl
{
    using System;
    using System.Collections.Generic;

    static class MetadataExtensions
    {
        public static T MaybeGet<T>(this IDictionary<string, object> metadata, string key)
        {
            object foundValue;
            if (metadata.TryGetValue(key, out foundValue))
            {
                return (T)foundValue;
            }
            return default(T);
        }

        public static string MaybeGetAsString(this IDictionary<string, object> metadata, string key)
        {
            object foundValue;
            if (metadata.TryGetValue(key, out foundValue))
            {
                return foundValue?.ToString();
            }

            return null;
        }

        public static DateTime? AsMaybeDateTime(this string s)
        {
            if (s == null) return null;
            DateTime dt;
            if (DateTime.TryParse(s, out dt))
            {
                return dt;
            }

            return null;
        }
    }
}