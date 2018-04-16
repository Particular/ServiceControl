namespace ServiceControl
{
    using System;
    using System.Collections.Generic;

    static class MetadataExtensions
    {
        public static T GetOrDefault<T>(this IDictionary<string, object> metadata, string key)
        {
            object foundValue;
            if (metadata.TryGetValue(key, out foundValue))
            {
                return (T)foundValue;
            }
            return default(T);
        }

        public static string GetAsStringOrNull(this IDictionary<string, object> metadata, string key)
        {
            object foundValue;
            if (metadata.TryGetValue(key, out foundValue))
            {
                return foundValue?.ToString();
            }

            return null;
        }

        public static DateTime? GetAsNullableDatetime(this IDictionary<string, object> metadata, string key)
        {
            var datetimeAsString = metadata.GetAsStringOrNull(key);
            DateTime dt;
            if (datetimeAsString != null && DateTime.TryParse(datetimeAsString, out dt))
            {
                return dt;
            }
            return null;
        }
    }
}