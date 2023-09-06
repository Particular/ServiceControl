namespace ServiceControl
{
    using System;
    using System.Collections.Generic;

    static class MetadataExtensions
    {
        public static T GetOrDefault<T>(this IDictionary<string, object> metadata, string key)
        {
            if (metadata.TryGetValue(key, out var foundValue))
            {
                return (T)foundValue;
            }

            return default;
        }

        public static string GetAsStringOrNull(this IDictionary<string, object> metadata, string key)
        {
            if (metadata.TryGetValue(key, out var foundValue))
            {
                return foundValue?.ToString();
            }

            return null;
        }

        public static DateTime? GetAsNullableDatetime(this IDictionary<string, object> metadata, string key)
        {
            var datetimeAsString = metadata.GetAsStringOrNull(key);

            if (datetimeAsString != null)
            {
                return DateTime.Parse(datetimeAsString);
            }

            return null;
        }
    }
}